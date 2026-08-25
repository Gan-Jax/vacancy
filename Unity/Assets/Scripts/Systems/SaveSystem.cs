using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vacancy
{
    [Serializable]
    public sealed class StaffSave
    {
        public bool Hired;
        public int WagesOwed;
        public int DaysWorkedInPeriod;
        public int PeriodDays;
        public bool PaydayDue;
        public bool WorkedToday;
    }

    public static class SaveSystem
    {
        const int Version = 1;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "vacancy-save.json");

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        public static bool Save(GameState state, PlayerActor player, StaffNpc bob, StaffNpc mary)
        {
            if (state == null) return false;
            try
            {
                var json = JsonUtility.ToJson(Capture(state, player, bob, mary), true);
                File.WriteAllText(FilePath, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                return false;
            }
        }

        public static bool Load(GameState state, PlayerActor player, out StaffSave bob, out StaffSave mary)
        {
            bob = null;
            mary = null;
            if (state == null || !Exists()) return false;
            try
            {
                var blob = JsonUtility.FromJson<SaveBlob>(File.ReadAllText(FilePath));
                if (blob == null || blob.Version < 1) return false;
                Apply(blob, state, player);
                bob = blob.Bob;
                mary = blob.Mary;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Load failed: " + e.Message);
                return false;
            }
        }

        public static void Repath(GameState state, HotelLayout layout)
        {
            if (state == null || layout == null) return;

            foreach (var guest in state.WaitingGuests)
            {
                if (guest.Path == null) guest.Path = new List<Point>();
                else guest.Path.Clear();
            }

            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Path == null) guest.Path = new List<Point>();
                else guest.Path.Clear();
                if (guest.Phase == "in_room" && guest.RoomId >= 1 && guest.RoomId <= state.Rooms.Count)
                {
                    var dest = layout.RoomInterior(guest.RoomId);
                    guest.X = dest.X;
                    guest.Y = dest.Y;
                    guest.Nav = null;
                }
            }

            foreach (var car in state.Cars)
            {
                if (car == null) continue;
                car.Waypoint = 0;
                if (car.Stage == "inbound") BindCarPath(car, layout.StallDriveIn(car.StallIndex));
                else if (car.Stage == "outbound") BindCarPath(car, layout.StallDriveOut(car.StallIndex));
                else
                {
                    car.Path = new List<Point>();
                    var pose = layout.StallPose(car.StallIndex);
                    car.X = pose.Car.X;
                    car.Y = pose.Car.Y;
                }
            }
        }

        static void BindCarPath(GuestCar car, List<Point> path)
        {
            car.Path = path ?? new List<Point>();
            car.Waypoint = 0;
            if (car.Path.Count == 0) return;
            float best = float.MaxValue;
            for (int i = 0; i < car.Path.Count; i++)
            {
                float d = Geometry.Dist(car.X, car.Y, car.Path[i].X, car.Path[i].Y);
                if (d < best)
                {
                    best = d;
                    car.Waypoint = i;
                }
            }
        }

        static SaveBlob Capture(GameState state, PlayerActor player, StaffNpc bob, StaffNpc mary)
        {
            var blob = new SaveBlob
            {
                Version = Version,
                Money = state.Money,
                Day = state.Day,
                Hour = state.Hour,
                Reputation = state.Reputation,
                VacancyOpen = state.VacancyOpen,
                BobHired = state.BobHired,
                MaryHired = state.MaryHired,
                Stage = state.Stage,
                NextRequestId = state.NextRequestId,
                Messages = state.Messages.ToArray(),
                Rooms = CaptureRooms(state),
                WaitingGuests = CaptureWaiting(state),
                ActiveGuests = CaptureGuests(state),
                Cars = CaptureCars(state),
                Requests = CaptureRequests(state),
                Inventory = CaptureInventory(state),
                Tutorial = CaptureTutorial(state),
                Story = CaptureStory(state),
                Shelter = CaptureShelter(state),
                Bob = CaptureStaff(state.BobHired, bob),
                Mary = CaptureStaff(state.MaryHired, mary)
            };

            if (player != null)
            {
                blob.HasPlayer = true;
                blob.PlayerX = player.X;
                blob.PlayerY = player.Y;
                blob.PlayerYaw = player.Yaw;
                blob.PlayerPitch = player.Pitch;
                blob.PlayerFloor = player.FloorLevel;
                blob.PlayerFootY = player.FootY;
            }

            return blob;
        }

        static void Apply(SaveBlob blob, GameState state, PlayerActor player)
        {
            state.Money = blob.Money;
            state.Day = blob.Day;
            state.Hour = blob.Hour;
            state.Reputation = blob.Reputation;
            state.VacancyOpen = blob.VacancyOpen;
            state.BobHired = blob.BobHired;
            state.MaryHired = blob.MaryHired;
            state.Stage = blob.Stage <= 0 ? 1 : blob.Stage;
            state.NextRequestId = blob.NextRequestId < 1 ? 1 : blob.NextRequestId;
            state.Paused = false;
            state.PauseMenuOpen = false;
            state.PcOpen = false;
            state.MediaOpen = null;
            state.DeskGuest = null;

            state.Messages.Clear();
            if (blob.Messages != null)
            {
                foreach (var line in blob.Messages) state.Messages.Add(line);
            }

            ApplyRooms(blob, state);
            ApplyWaiting(blob, state);
            ApplyGuests(blob, state);
            ApplyCars(blob, state);
            ApplyRequests(blob, state);
            state.Inventory = ApplyInventory(blob);
            state.Tutorial = ApplyTutorial(blob);
            state.Story = ApplyStory(blob);
            state.Shelter = ApplyShelter(blob);

            if (blob.HasPlayer && player != null)
            {
                player.X = blob.PlayerX;
                player.Y = blob.PlayerY;
                player.Yaw = blob.PlayerYaw;
                player.Pitch = blob.PlayerPitch;
                player.FloorLevel = blob.PlayerFloor;
                player.FootY = blob.PlayerFootY;
                player.ActiveTask = null;
                player.Path = new List<Point>();
            }
        }

        static RoomSave[] CaptureRooms(GameState state)
        {
            var list = new List<RoomSave>();
            foreach (var room in state.Rooms) list.Add(ToSave(room));
            return list.ToArray();
        }

        static void ApplyRooms(SaveBlob blob, GameState state)
        {
            if (blob.Rooms == null) return;
            foreach (var save in blob.Rooms)
            {
                if (save == null) continue;
                Room room = null;
                foreach (var existing in state.Rooms)
                {
                    if (existing.Id == save.Id)
                    {
                        room = existing;
                        break;
                    }
                }

                if (room == null) continue;
                FromSave(save, room);
            }
        }

        static RoomSave ToSave(Room room)
        {
            return new RoomSave
            {
                Id = room.Id,
                Unlocked = room.Unlocked,
                Status = room.Status,
                GuestName = room.GuestName,
                HasStayRemaining = room.StayRemainingHours != null,
                StayRemainingHours = room.StayRemainingHours ?? 0,
                HasStayDays = room.StayDays != null,
                StayDays = room.StayDays ?? 0,
                HasPaymentsLeft = room.PaymentsLeft != null,
                PaymentsLeft = room.PaymentsLeft ?? 0,
                HasNextInterval = room.NextIntervalPaymentIn != null,
                NextIntervalPaymentIn = room.NextIntervalPaymentIn ?? 0,
                HasHiddenDamage = room.HasHiddenDamage,
                DamageFound = room.DamageFound,
                DirtLevel = room.DirtLevel,
                RepairLevel = room.RepairLevel,
                RepairPaid = room.RepairPaid,
                HasRepairCost = room.RepairCost != null,
                RepairCost = room.RepairCost ?? 0,
                CleanProgress = room.CleanProgress,
                InspectProgress = room.InspectProgress,
                RepairProgress = room.RepairProgress,
                StayCount = room.StayCount,
                StaysSinceTowel = room.StaysSinceTowel,
                TpDayCounter = room.TpDayCounter
            };
        }

        static void FromSave(RoomSave save, Room room)
        {
            room.Unlocked = save.Unlocked;
            room.Status = save.Status ?? "clean";
            room.GuestName = save.GuestName;
            room.StayRemainingHours = save.HasStayRemaining ? save.StayRemainingHours : (float?)null;
            room.StayDays = save.HasStayDays ? save.StayDays : (int?)null;
            room.PaymentsLeft = save.HasPaymentsLeft ? save.PaymentsLeft : (int?)null;
            room.NextIntervalPaymentIn = save.HasNextInterval ? save.NextIntervalPaymentIn : (float?)null;
            room.HasHiddenDamage = save.HasHiddenDamage;
            room.DamageFound = save.DamageFound;
            room.DirtLevel = save.DirtLevel;
            room.RepairLevel = save.RepairLevel;
            room.RepairPaid = save.RepairPaid;
            room.RepairCost = save.HasRepairCost ? save.RepairCost : (int?)null;
            room.CleanProgress = save.CleanProgress;
            room.InspectProgress = save.InspectProgress;
            room.RepairProgress = save.RepairProgress;
            room.Worker = null;
            room.StayCount = save.StayCount;
            room.StaysSinceTowel = save.StaysSinceTowel;
            room.TpDayCounter = save.TpDayCounter;
        }

        static WaitingSave[] CaptureWaiting(GameState state)
        {
            var list = new List<WaitingSave>();
            foreach (var guest in state.WaitingGuests) list.Add(ToSave(guest));
            return list.ToArray();
        }

        static void ApplyWaiting(SaveBlob blob, GameState state)
        {
            state.WaitingGuests.Clear();
            if (blob.WaitingGuests == null) return;
            foreach (var save in blob.WaitingGuests)
            {
                if (save != null) state.WaitingGuests.Add(FromSave(save));
            }
        }

        static WaitingSave ToSave(WaitingGuest guest)
        {
            var signs = new List<SignSave>();
            foreach (var sign in guest.Signs)
            {
                signs.Add(new SignSave { Text = sign.Text, Damning = sign.Damning, Revealed = sign.Revealed });
            }

            var replies = new List<ReplySave>();
            foreach (var reply in guest.Replies)
            {
                replies.Add(new ReplySave { Prompt = reply.Prompt, Spoken = reply.Spoken, Source = reply.Source });
            }

            return new WaitingSave
            {
                Name = guest.Name,
                Kind = guest.Kind,
                StoryId = guest.StoryId,
                Claim = guest.Claim,
                Signs = signs.ToArray(),
                QuestionsAsked = guest.QuestionsAsked,
                MaxQuestions = guest.MaxQuestions,
                AskedQuestionIds = guest.AskedQuestionIds.ToArray(),
                WaitRemainingHours = guest.WaitRemainingHours,
                Marked = guest.Marked,
                Tell = guest.Tell,
                Replies = replies.ToArray(),
                X = guest.X,
                Y = guest.Y,
                Radius = guest.Radius,
                FloorLevel = guest.FloorLevel,
                FootY = guest.FootY,
                ArrivePhase = guest.ArrivePhase,
                StallIndex = guest.StallIndex,
                CarColor = guest.CarColor,
                BoughtPaper = guest.BoughtPaper,
                PaperOffered = guest.PaperOffered
            };
        }

        static WaitingGuest FromSave(WaitingSave save)
        {
            var guest = new WaitingGuest
            {
                Name = save.Name,
                Kind = string.IsNullOrEmpty(save.Kind) ? GuestKind.Traveler : save.Kind,
                StoryId = save.StoryId,
                Claim = save.Claim,
                QuestionsAsked = save.QuestionsAsked,
                MaxQuestions = save.MaxQuestions > 0 ? save.MaxQuestions : 2,
                WaitRemainingHours = save.WaitRemainingHours,
                Marked = save.Marked,
                Tell = save.Tell,
                X = save.X,
                Y = save.Y,
                Radius = save.Radius > 0 ? save.Radius : 11f,
                FloorLevel = save.FloorLevel,
                FootY = save.FootY,
                ArrivePhase = save.ArrivePhase,
                StallIndex = save.StallIndex,
                CarColor = save.CarColor,
                BoughtPaper = save.BoughtPaper,
                PaperOffered = save.PaperOffered,
                Path = new List<Point>()
            };

            if (save.Signs != null)
            {
                foreach (var sign in save.Signs)
                {
                    guest.Signs.Add(new GuestSign { Text = sign.Text, Damning = sign.Damning, Revealed = sign.Revealed });
                }
            }

            if (save.Replies != null)
            {
                foreach (var reply in save.Replies)
                {
                    guest.Replies.Add(new GuestReply { Prompt = reply.Prompt, Spoken = reply.Spoken, Source = reply.Source });
                }
            }

            if (save.AskedQuestionIds != null)
            {
                guest.AskedQuestionIds.AddRange(save.AskedQuestionIds);
            }

            return guest;
        }

        static GuestSave[] CaptureGuests(GameState state)
        {
            var list = new List<GuestSave>();
            foreach (var guest in state.ActiveGuests) list.Add(ToSave(guest));
            return list.ToArray();
        }

        static void ApplyGuests(SaveBlob blob, GameState state)
        {
            state.ActiveGuests.Clear();
            if (blob.ActiveGuests == null) return;
            foreach (var save in blob.ActiveGuests)
            {
                if (save != null) state.ActiveGuests.Add(FromSave(save));
            }
        }

        static GuestSave ToSave(Guest guest)
        {
            return new GuestSave
            {
                Name = guest.Name,
                Kind = guest.Kind,
                Marked = guest.Marked,
                Phase = guest.Phase,
                Nav = guest.Nav,
                RoomId = guest.RoomId,
                X = guest.X,
                Y = guest.Y,
                Radius = guest.Radius,
                TargetX = guest.TargetX,
                TargetY = guest.TargetY,
                StayDays = guest.StayDays,
                StayRemainingHours = guest.StayRemainingHours,
                PaymentsLeft = guest.PaymentsLeft,
                NextIntervalPaymentIn = guest.NextIntervalPaymentIn,
                HasHiddenDamage = guest.HasHiddenDamage,
                HasWaitRemaining = guest.WaitRemainingHours != null,
                WaitRemainingHours = guest.WaitRemainingHours ?? 0,
                HasReputationBonus = guest.ReputationBonus != null,
                ReputationBonus = guest.ReputationBonus ?? 0,
                UpsetCheckout = guest.UpsetCheckout,
                FloorLevel = guest.FloorLevel,
                FootY = guest.FootY,
                StallIndex = guest.StallIndex,
                CarColor = guest.CarColor,
                BoughtPaper = guest.BoughtPaper,
                PaperOffered = guest.PaperOffered,
                HasPaperTripIn = guest.PaperTripIn != null,
                PaperTripIn = guest.PaperTripIn ?? 0,
                HasRequestRollIn = guest.RequestRollIn != null,
                RequestRollIn = guest.RequestRollIn ?? 0,
                HasRequested = guest.HasRequested,
                HasWalkaboutIn = guest.WalkaboutIn != null,
                WalkaboutIn = guest.WalkaboutIn ?? 0,
                DidWalkabout = guest.DidWalkabout,
                WalkLingerSeconds = guest.WalkLingerSeconds
            };
        }

        static Guest FromSave(GuestSave save)
        {
            return new Guest
            {
                Name = save.Name,
                Kind = string.IsNullOrEmpty(save.Kind) ? GuestKind.Traveler : save.Kind,
                Marked = save.Marked,
                Phase = save.Phase,
                Nav = save.Nav,
                RoomId = save.RoomId,
                X = save.X,
                Y = save.Y,
                Radius = save.Radius > 0 ? save.Radius : 11f,
                Path = new List<Point>(),
                TargetX = save.TargetX,
                TargetY = save.TargetY,
                StayDays = save.StayDays,
                StayRemainingHours = save.StayRemainingHours,
                PaymentsLeft = save.PaymentsLeft,
                NextIntervalPaymentIn = save.NextIntervalPaymentIn,
                HasHiddenDamage = save.HasHiddenDamage,
                WaitRemainingHours = save.HasWaitRemaining ? save.WaitRemainingHours : (float?)null,
                ReputationBonus = save.HasReputationBonus ? save.ReputationBonus : (int?)null,
                UpsetCheckout = save.UpsetCheckout,
                FloorLevel = save.FloorLevel,
                FootY = save.FootY,
                StallIndex = save.StallIndex,
                CarColor = save.CarColor,
                BoughtPaper = save.BoughtPaper,
                PaperOffered = save.PaperOffered,
                PaperTripIn = save.HasPaperTripIn ? save.PaperTripIn : (float?)null,
                RequestRollIn = save.HasRequestRollIn ? save.RequestRollIn : (float?)null,
                HasRequested = save.HasRequested,
                WalkaboutIn = save.HasWalkaboutIn ? save.WalkaboutIn : (float?)null,
                DidWalkabout = save.DidWalkabout,
                WalkLingerSeconds = save.WalkLingerSeconds
            };
        }

        static CarSave[] CaptureCars(GameState state)
        {
            var list = new List<CarSave>();
            foreach (var car in state.Cars)
            {
                list.Add(new CarSave
                {
                    Owner = car.Owner,
                    StallIndex = car.StallIndex,
                    X = car.X,
                    Y = car.Y,
                    Color = car.Color,
                    Stage = car.Stage,
                    Waypoint = car.Waypoint
                });
            }

            return list.ToArray();
        }

        static void ApplyCars(SaveBlob blob, GameState state)
        {
            state.Cars.Clear();
            if (blob.Cars == null) return;
            foreach (var save in blob.Cars)
            {
                if (save == null) continue;
                state.Cars.Add(new GuestCar
                {
                    Owner = save.Owner,
                    StallIndex = save.StallIndex,
                    X = save.X,
                    Y = save.Y,
                    Color = save.Color,
                    Stage = string.IsNullOrEmpty(save.Stage) ? "parked" : save.Stage,
                    Waypoint = save.Waypoint,
                    Path = new List<Point>()
                });
            }
        }

        static RequestSave[] CaptureRequests(GameState state)
        {
            var list = new List<RequestSave>();
            foreach (var req in state.Requests)
            {
                list.Add(new RequestSave
                {
                    Id = req.Id,
                    RoomId = req.RoomId,
                    GuestName = req.GuestName,
                    Kind = req.Kind,
                    Label = req.Label,
                    SupplyId = req.SupplyId,
                    HoursLeft = req.HoursLeft
                });
            }

            return list.ToArray();
        }

        static void ApplyRequests(SaveBlob blob, GameState state)
        {
            state.Requests.Clear();
            if (blob.Requests == null) return;
            foreach (var save in blob.Requests)
            {
                if (save == null) continue;
                state.Requests.Add(new GuestRequest
                {
                    Id = save.Id,
                    RoomId = save.RoomId,
                    GuestName = save.GuestName,
                    Kind = save.Kind,
                    Label = save.Label,
                    SupplyId = save.SupplyId,
                    HoursLeft = save.HoursLeft
                });
            }
        }

        static InventorySave CaptureInventory(GameState state)
        {
            var inv = state.Inventory ?? InventorySystem.Create();
            var orders = new List<OrderSave>();
            foreach (var order in inv.PendingOrders)
            {
                orders.Add(new OrderSave
                {
                    Id = order.Id,
                    Items = ToPairs(order.Items),
                    Cost = order.Cost,
                    HoursLeft = order.HoursLeft
                });
            }

            return new InventorySave
            {
                Stock = ToPairs(inv.Stock),
                PendingOrders = orders.ToArray(),
                NextOrderId = inv.NextOrderId
            };
        }

        static InventoryState ApplyInventory(SaveBlob blob)
        {
            var inv = InventorySystem.Create();
            if (blob.Inventory == null) return inv;
            ApplyPairs(blob.Inventory.Stock, inv.Stock);
            inv.NextOrderId = blob.Inventory.NextOrderId < 1 ? 1 : blob.Inventory.NextOrderId;
            inv.PendingOrders.Clear();
            if (blob.Inventory.PendingOrders != null)
            {
                foreach (var save in blob.Inventory.PendingOrders)
                {
                    if (save == null) continue;
                    inv.PendingOrders.Add(new PendingOrder
                    {
                        Id = save.Id,
                        Items = FromPairs(save.Items),
                        Cost = save.Cost,
                        HoursLeft = save.HoursLeft
                    });
                }
            }

            return inv;
        }

        static TutorialSave CaptureTutorial(GameState state)
        {
            var t = state.Tutorial ?? new TutorialProgress();
            return new TutorialSave
            {
                CheckIn = t.CheckIn,
                VacancySign = t.VacancySign,
                RoomWork = t.RoomWork,
                HireStaff = t.HireStaff,
                OfficePc = t.OfficePc
            };
        }

        static TutorialProgress ApplyTutorial(SaveBlob blob)
        {
            var t = new TutorialProgress();
            if (blob.Tutorial == null) return t;
            t.CheckIn = blob.Tutorial.CheckIn;
            t.VacancySign = blob.Tutorial.VacancySign;
            t.RoomWork = blob.Tutorial.RoomWork;
            t.HireStaff = blob.Tutorial.HireStaff;
            t.OfficePc = blob.Tutorial.OfficePc;
            return t;
        }

        static StorySave CaptureStory(GameState state)
        {
            var story = state.Story ?? Story.Create();
            var threats = new List<ThreatSave>();
            foreach (var item in story.PendingThreats)
            {
                threats.Add(new ThreatSave { Name = item.Name, AdmittedDay = item.AdmittedDay, FireOnDay = item.FireOnDay });
            }

            var vind = new List<VindicationSave>();
            foreach (var item in story.PendingVindication)
            {
                vind.Add(new VindicationSave { Name = item.Name, Day = item.Day });
            }

            var dispatches = new List<DispatchSave>();
            foreach (var item in story.Dispatches)
            {
                dispatches.Add(new DispatchSave { Day = item.Day, Text = item.Text });
            }

            return new StorySave
            {
                Act = story.Act,
                Fired = ToIntPairs(story.Fired),
                Flags = ToBoolPairs(story.Flags),
                Tension = story.Tension,
                Humanity = story.Humanity,
                PendingThreats = threats.ToArray(),
                PendingVindication = vind.ToArray(),
                ThreatsRefused = story.ThreatsRefused,
                HintIn = story.HintIn,
                Dispatches = dispatches.ToArray(),
                Banner = story.Banner == null
                    ? null
                    : new BannerSave { Title = story.Banner.Title, Body = story.Banner.Body, Act = story.Banner.Act },
                Stats = story.Stats == null
                    ? new StatsSave()
                    : new StatsSave
                    {
                        CheckIns = story.Stats.CheckIns,
                        CheckOuts = story.Stats.CheckOuts,
                        TurnedAway = story.Stats.TurnedAway,
                        MarkedServed = story.Stats.MarkedServed,
                        MarkedRefused = story.Stats.MarkedRefused,
                        Repairs = story.Stats.Repairs,
                        Orders = story.Stats.Orders
                    },
                Media = CaptureMedia(story.Media)
            };
        }

        static StoryState ApplyStory(SaveBlob blob)
        {
            var story = Story.Create();
            if (blob.Story == null) return story;
            var save = blob.Story;
            story.Act = string.IsNullOrEmpty(save.Act) ? "normalcy" : save.Act;
            ApplyIntPairs(save.Fired, story.Fired);
            ApplyBoolPairs(save.Flags, story.Flags);
            story.Tension = save.Tension;
            story.Humanity = save.Humanity;
            story.ThreatsRefused = save.ThreatsRefused;
            story.HintIn = save.HintIn;
            story.PendingThreats.Clear();
            if (save.PendingThreats != null)
            {
                foreach (var item in save.PendingThreats)
                {
                    if (item == null) continue;
                    story.PendingThreats.Add(new PendingThreat
                    {
                        Name = item.Name,
                        AdmittedDay = item.AdmittedDay,
                        FireOnDay = item.FireOnDay
                    });
                }
            }

            story.PendingVindication.Clear();
            if (save.PendingVindication != null)
            {
                foreach (var item in save.PendingVindication)
                {
                    if (item == null) continue;
                    story.PendingVindication.Add(new PendingVindication { Name = item.Name, Day = item.Day });
                }
            }

            story.Dispatches.Clear();
            if (save.Dispatches != null)
            {
                foreach (var item in save.Dispatches)
                {
                    if (item == null) continue;
                    story.Dispatches.Add(new StoryDispatch { Day = item.Day, Text = item.Text });
                }
            }

            if (save.Banner != null && !string.IsNullOrEmpty(save.Banner.Title))
            {
                story.Banner = new StoryBanner
                {
                    Title = save.Banner.Title,
                    Body = save.Banner.Body,
                    Act = save.Banner.Act
                };
            }

            if (save.Stats != null)
            {
                story.Stats.CheckIns = save.Stats.CheckIns;
                story.Stats.CheckOuts = save.Stats.CheckOuts;
                story.Stats.TurnedAway = save.Stats.TurnedAway;
                story.Stats.MarkedServed = save.Stats.MarkedServed;
                story.Stats.MarkedRefused = save.Stats.MarkedRefused;
                story.Stats.Repairs = save.Stats.Repairs;
                story.Stats.Orders = save.Stats.Orders;
            }

            story.Media = ApplyMedia(save.Media);
            return story;
        }

        static MediaSave CaptureMedia(MediaState media)
        {
            media = media ?? Media.Create();
            var radio = new List<RadioSave>();
            foreach (var entry in media.RadioLog)
            {
                radio.Add(new RadioSave
                {
                    Id = entry.Id,
                    Day = entry.Day,
                    Headline = entry.Headline,
                    Body = entry.Body,
                    Kind = entry.Kind
                });
            }

            var papers = new List<PaperSave>();
            foreach (var entry in media.Papers)
            {
                papers.Add(new PaperSave
                {
                    Id = entry.Id,
                    Day = entry.Day,
                    Headline = entry.Headline,
                    Body = entry.Body,
                    Kind = entry.Kind,
                    Read = entry.Read
                });
            }

            return new MediaSave
            {
                RadioLog = radio.ToArray(),
                Papers = papers.ToArray(),
                AiredIds = media.AiredIds.ToArray(),
                PrintedIds = media.PrintedIds.ToArray(),
                RadioIn = media.RadioIn,
                LastPaperDay = media.LastPaperDay
            };
        }

        static MediaState ApplyMedia(MediaSave save)
        {
            var media = Media.Create();
            if (save == null) return media;
            media.RadioIn = save.RadioIn;
            media.LastPaperDay = save.LastPaperDay;
            if (save.RadioLog != null)
            {
                foreach (var entry in save.RadioLog)
                {
                    if (entry == null) continue;
                    media.RadioLog.Add(new RadioEntry
                    {
                        Id = entry.Id,
                        Day = entry.Day,
                        Headline = entry.Headline,
                        Body = entry.Body,
                        Kind = entry.Kind
                    });
                }
            }

            if (save.Papers != null)
            {
                foreach (var entry in save.Papers)
                {
                    if (entry == null) continue;
                    media.Papers.Add(new PaperIssue
                    {
                        Id = entry.Id,
                        Day = entry.Day,
                        Headline = entry.Headline,
                        Body = entry.Body,
                        Kind = entry.Kind,
                        Read = entry.Read
                    });
                }
            }

            if (save.AiredIds != null) media.AiredIds.AddRange(save.AiredIds);
            if (save.PrintedIds != null) media.PrintedIds.AddRange(save.PrintedIds);
            return media;
        }

        static ShelterSave CaptureShelter(GameState state)
        {
            var shelter = state.Shelter ?? Shelter.Create();
            return new ShelterSave
            {
                Unlocked = shelter.Unlocked,
                DefenseActive = shelter.DefenseActive,
                Stock = ToPairs(shelter.Stock),
                Integrity = shelter.Integrity,
                Powered = shelter.Powered,
                DaysHeld = shelter.DaysHeld,
                LastShortage = shelter.LastShortage
            };
        }

        static ShelterState ApplyShelter(SaveBlob blob)
        {
            var shelter = Shelter.Create();
            if (blob.Shelter == null) return shelter;
            shelter.Unlocked = blob.Shelter.Unlocked;
            shelter.DefenseActive = blob.Shelter.DefenseActive;
            ApplyPairs(blob.Shelter.Stock, shelter.Stock);
            shelter.Integrity = blob.Shelter.Integrity;
            shelter.Powered = blob.Shelter.Powered;
            shelter.DaysHeld = blob.Shelter.DaysHeld;
            shelter.LastShortage = blob.Shelter.LastShortage;
            return shelter;
        }

        static StaffSave CaptureStaff(bool hired, StaffNpc npc)
        {
            var save = new StaffSave { Hired = hired };
            if (npc == null) return save;
            save.WagesOwed = npc.WagesOwed;
            save.DaysWorkedInPeriod = npc.DaysWorkedInPeriod;
            save.PeriodDays = npc.PeriodDays;
            save.PaydayDue = npc.PaydayDue;
            save.WorkedToday = npc.WorkedToday;
            return save;
        }

        static KeyInt[] ToPairs(Dictionary<string, int> dict)
        {
            if (dict == null) return Array.Empty<KeyInt>();
            var list = new List<KeyInt>();
            foreach (var pair in dict) list.Add(new KeyInt { Key = pair.Key, Value = pair.Value });
            return list.ToArray();
        }

        static KeyInt[] ToIntPairs(Dictionary<string, int> dict) => ToPairs(dict);

        static Dictionary<string, int> FromPairs(KeyInt[] pairs)
        {
            var dict = new Dictionary<string, int>();
            ApplyPairs(pairs, dict);
            return dict;
        }

        static void ApplyPairs(KeyInt[] pairs, Dictionary<string, int> dest)
        {
            if (pairs == null || dest == null) return;
            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.Key)) continue;
                dest[pair.Key] = pair.Value;
            }
        }

        static KeyBool[] ToBoolPairs(Dictionary<string, bool> dict)
        {
            if (dict == null) return Array.Empty<KeyBool>();
            var list = new List<KeyBool>();
            foreach (var pair in dict) list.Add(new KeyBool { Key = pair.Key, Value = pair.Value });
            return list.ToArray();
        }

        static void ApplyBoolPairs(KeyBool[] pairs, Dictionary<string, bool> dest)
        {
            if (pairs == null || dest == null) return;
            dest.Clear();
            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.Key)) continue;
                dest[pair.Key] = pair.Value;
            }
        }

        static void ApplyIntPairs(KeyInt[] pairs, Dictionary<string, int> dest)
        {
            if (dest == null) return;
            dest.Clear();
            ApplyPairs(pairs, dest);
        }

        [Serializable]
        class SaveBlob
        {
            public int Version;
            public int Money;
            public int Day;
            public float Hour;
            public int Reputation;
            public bool VacancyOpen;
            public bool BobHired;
            public bool MaryHired;
            public int Stage;
            public int NextRequestId;
            public string[] Messages;
            public RoomSave[] Rooms;
            public WaitingSave[] WaitingGuests;
            public GuestSave[] ActiveGuests;
            public CarSave[] Cars;
            public RequestSave[] Requests;
            public InventorySave Inventory;
            public TutorialSave Tutorial;
            public StorySave Story;
            public ShelterSave Shelter;
            public StaffSave Bob;
            public StaffSave Mary;
            public bool HasPlayer;
            public float PlayerX;
            public float PlayerY;
            public float PlayerYaw;
            public float PlayerPitch;
            public int PlayerFloor;
            public float PlayerFootY;
        }

        [Serializable]
        class RoomSave
        {
            public int Id;
            public bool Unlocked;
            public string Status;
            public string GuestName;
            public bool HasStayRemaining;
            public float StayRemainingHours;
            public bool HasStayDays;
            public int StayDays;
            public bool HasPaymentsLeft;
            public int PaymentsLeft;
            public bool HasNextInterval;
            public float NextIntervalPaymentIn;
            public bool HasHiddenDamage;
            public bool DamageFound;
            public string DirtLevel;
            public string RepairLevel;
            public bool RepairPaid;
            public bool HasRepairCost;
            public int RepairCost;
            public float CleanProgress;
            public float InspectProgress;
            public float RepairProgress;
            public int StayCount;
            public int StaysSinceTowel;
            public int TpDayCounter;
        }

        [Serializable]
        class WaitingSave
        {
            public string Name;
            public string Kind;
            public string StoryId;
            public string Claim;
            public SignSave[] Signs;
            public int QuestionsAsked;
            public int MaxQuestions;
            public string[] AskedQuestionIds;
            public float WaitRemainingHours;
            public bool Marked;
            public string Tell;
            public ReplySave[] Replies;
            public float X;
            public float Y;
            public float Radius;
            public int FloorLevel;
            public float FootY;
            public string ArrivePhase;
            public int StallIndex;
            public string CarColor;
            public bool BoughtPaper;
            public bool PaperOffered;
        }

        [Serializable]
        class GuestSave
        {
            public string Name;
            public string Kind;
            public bool Marked;
            public string Phase;
            public string Nav;
            public int RoomId;
            public float X;
            public float Y;
            public float Radius;
            public float TargetX;
            public float TargetY;
            public int StayDays;
            public float StayRemainingHours;
            public int PaymentsLeft;
            public float NextIntervalPaymentIn;
            public bool HasHiddenDamage;
            public bool HasWaitRemaining;
            public float WaitRemainingHours;
            public bool HasReputationBonus;
            public int ReputationBonus;
            public bool UpsetCheckout;
            public int FloorLevel;
            public float FootY;
            public int StallIndex;
            public string CarColor;
            public bool BoughtPaper;
            public bool PaperOffered;
            public bool HasPaperTripIn;
            public float PaperTripIn;
            public bool HasRequestRollIn;
            public float RequestRollIn;
            public bool HasRequested;
            public bool HasWalkaboutIn;
            public float WalkaboutIn;
            public bool DidWalkabout;
            public float WalkLingerSeconds;
        }

        [Serializable]
        class CarSave
        {
            public string Owner;
            public int StallIndex;
            public float X;
            public float Y;
            public string Color;
            public string Stage;
            public int Waypoint;
        }

        [Serializable]
        class RequestSave
        {
            public string Id;
            public int RoomId;
            public string GuestName;
            public string Kind;
            public string Label;
            public string SupplyId;
            public float HoursLeft;
        }

        [Serializable]
        class InventorySave
        {
            public KeyInt[] Stock;
            public OrderSave[] PendingOrders;
            public int NextOrderId;
        }

        [Serializable]
        class OrderSave
        {
            public string Id;
            public KeyInt[] Items;
            public int Cost;
            public float HoursLeft;
        }

        [Serializable]
        class TutorialSave
        {
            public bool CheckIn;
            public bool VacancySign;
            public bool RoomWork;
            public bool HireStaff;
            public bool OfficePc;
        }

        [Serializable]
        class StorySave
        {
            public string Act;
            public KeyInt[] Fired;
            public KeyBool[] Flags;
            public float Tension;
            public int Humanity;
            public ThreatSave[] PendingThreats;
            public VindicationSave[] PendingVindication;
            public int ThreatsRefused;
            public float HintIn;
            public DispatchSave[] Dispatches;
            public BannerSave Banner;
            public StatsSave Stats;
            public MediaSave Media;
        }

        [Serializable]
        class MediaSave
        {
            public RadioSave[] RadioLog;
            public PaperSave[] Papers;
            public string[] AiredIds;
            public string[] PrintedIds;
            public float RadioIn;
            public int LastPaperDay;
        }

        [Serializable]
        class ShelterSave
        {
            public bool Unlocked;
            public bool DefenseActive;
            public KeyInt[] Stock;
            public float Integrity;
            public bool Powered;
            public int DaysHeld;
            public string LastShortage;
        }

        [Serializable]
        class SignSave
        {
            public string Text;
            public bool Damning;
            public bool Revealed;
        }

        [Serializable]
        class ReplySave
        {
            public string Prompt;
            public string Spoken;
            public string Source;
        }

        [Serializable]
        class ThreatSave
        {
            public string Name;
            public int AdmittedDay;
            public int FireOnDay;
        }

        [Serializable]
        class VindicationSave
        {
            public string Name;
            public int Day;
        }

        [Serializable]
        class DispatchSave
        {
            public int Day;
            public string Text;
        }

        [Serializable]
        class BannerSave
        {
            public string Title;
            public string Body;
            public string Act;
        }

        [Serializable]
        class StatsSave
        {
            public int CheckIns;
            public int CheckOuts;
            public int TurnedAway;
            public int MarkedServed;
            public int MarkedRefused;
            public int Repairs;
            public int Orders;
        }

        [Serializable]
        class RadioSave
        {
            public string Id;
            public int Day;
            public string Headline;
            public string Body;
            public string Kind;
        }

        [Serializable]
        class PaperSave
        {
            public string Id;
            public int Day;
            public string Headline;
            public string Body;
            public string Kind;
            public bool Read;
        }

        [Serializable]
        class KeyInt
        {
            public string Key;
            public int Value;
        }

        [Serializable]
        class KeyBool
        {
            public string Key;
            public bool Value;
        }
    }
}
