using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace JGRAN_Tempgroup_plugin
{
    [ApiVersion(2, 1)]
    public class JGRAN_Tempgroup_plugin : TerrariaPlugin
    {
        public override string Author => "Rondeo Balos";
        public override string Description => "Recreating Tshock's TempGroup adding time continous feature even if the player is offline";
        public override string Name => "JGRAN TempGroup Plugin";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public JGRAN_Tempgroup_plugin(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, onInit);
			TShockAPI.Hooks.PlayerHooks.PlayerPostLogin.Register(this, onJoin);
			//TShockAPI.GetDataHandlers.PlayerPostLogin += onJoin;
			ServerApi.Hooks.ServerLeave.Register(this, onLeave);
		}

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, onInit);
				TShockAPI.Hooks.PlayerHooks.PlayerPostLogin.Deregister(this, onJoin);
				//TShockAPI.GetDataHandlers.PlayerPostLogin -= onJoin;
				ServerApi.Hooks.ServerLeave.Deregister(this, onLeave);
			}
            base.Dispose(disposing);
        }

        void onInit(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("jgran.settempgroup", TempGroup, "settempgroup")
            {
                HelpText = "Temporarily sets another player's group."
            });
        }

		private async static void onJoin(PlayerPostLoginEventArgs args /*object sender, TShockAPI.GetDataHandlers.PlayerPostLoginEventArgs args*/)
        {

			DateTime now = DateTime.Now;

			string name = Main.player[args.Who].name;
			string gt;

			if (!offline.TryGetValue(name, out gt)) { return; }
			List<TSPlayer> ply = TSPlayer.FindByNameOrID(name);
			string[] grouptime = gt.Split('|');

			DateTime elapse = DateTime.Parse(grouptime[2]);

			int time;
			if (!Int32.TryParse(grouptime[3], out time)) return;

			await Task.Delay(1000);

			if (time - now.Subtract(elapse).Seconds > 0)
			{
				Group group = TShock.Groups.GetGroupByName(grouptime[0]);

				ply[0].tempGroupTimer = new System.Timers.Timer((time - now.Subtract(elapse).Seconds) * 1000);
				ply[0].tempGroupTimer.Elapsed += ply[0].TempGroupTimerElapsed;
				ply[0].tempGroupTimer.Start();

				ply[0].tempGroup = group;

				ply[0].SendSuccessMessage(String.Format("Your group has been changed to {0} for {1}s", group.Name, time - now.Subtract(elapse).Seconds));

				online.Remove(name);
				online.Add(name, gt);
			}

			offline.Remove(name);
		}

		private static void onLeave(LeaveEventArgs args)
        {
			string name = Main.player[args.Who].name;
			List<TSPlayer> ply = TSPlayer.FindByNameOrID(Main.player[args.Who].name);

			DateTime now = DateTime.Now;

			string gt;
			if (!online.TryGetValue(name, out gt)) return;
			string[] grouptime = gt.Split('|');

			DateTime elapse = DateTime.Parse(grouptime[2]);

			int time;
			if (!Int32.TryParse(grouptime[3], out time)) return;

			if (time - now.Subtract(elapse).Seconds > 0)
            {
				offline.Remove(name);
				offline.Add(name, gt);
            }

			online.Remove(name);
        }

		private static Dictionary<string, string> offline = new Dictionary<string, string>();
		private static Dictionary<string, string> online = new Dictionary<string, string>();

		private static void TempGroup(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendInfoMessage("Invalid usage");
				args.Player.SendInfoMessage("Usage: {0}settempgroup <username> <new group> [time]", Commands.Specifier);
				return;
			}

			if (!TShock.Groups.GroupExists(args.Parameters[1]))
			{
				args.Player.SendErrorMessage("Could not find group {0}", args.Parameters[1]);
				return;
			}

			List<TSPlayer> ply = TSPlayer.FindByNameOrID(args.Parameters[0]);
			if (ply.Count < 1)
			{
				//offline or whatever
				int time;
				if (!TShock.Utils.TryParseTime(args.Parameters[2], out time))
				{
					args.Player.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
					args.Player.SendErrorMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
					return;
				}
				args.Player.SendInfoMessage("User {0} is offline. Reward has been granted and executed.", args.Parameters[0]);
				offline.Remove(args.Parameters[0]);
				offline.Add(args.Parameters[0], String.Format("{0}|{1}|{2}|{3}", args.Parameters[1], args.Parameters[2], DateTime.Now, time));
				return;
			}

			if (ply.Count > 1)
			{
				args.Player.SendMultipleMatchError(ply.Select(p => p.Account.Name));
			}

			if (args.Parameters.Count > 2)
			{
				int time;
				if (!TShock.Utils.TryParseTime(args.Parameters[2], out time))
				{
					args.Player.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
					args.Player.SendErrorMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
					return;
				}

				ply[0].tempGroupTimer = new System.Timers.Timer(time * 1000);
				ply[0].tempGroupTimer.Elapsed += ply[0].TempGroupTimerElapsed;
				ply[0].tempGroupTimer.Start();

				online.Remove(args.Parameters[0]);
				online.Add(args.Parameters[0], String.Format("{0}|{1}|{2}|{3}", args.Parameters[1], args.Parameters[2], DateTime.Now, time));
			}

			Group g = TShock.Groups.GetGroupByName(args.Parameters[1]);

			ply[0].tempGroup = g;


			if (args.Parameters.Count < 3)
			{
				args.Player.SendSuccessMessage(String.Format("You have changed {0}'s group to {1}", ply[0].Name, g.Name));
				ply[0].SendSuccessMessage(String.Format("Your group has temporarily been changed to {0}", g.Name));
			}
			else
			{
				args.Player.SendSuccessMessage(String.Format("You have changed {0}'s group to {1} for {2}",
					ply[0].Name, g.Name, args.Parameters[2]));
				ply[0].SendSuccessMessage(String.Format("Your group has been changed to {0} for {1}",
					g.Name, args.Parameters[2]));
			}
		}
	}
}
