using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;


namespace Plugin
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {

        #region Plugin Info
        public override string Author => "hufang360";
        public override string Description => "永夜控制";
        public override string Name => "LongNight";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        #endregion

        private int nightTotalDays = 8;
        private int nightRemainDays = 0;
        private bool nightEnable = true;
        private bool isInit = false;

        public Plugin(Main game) : base(game)
        {
            nightRemainDays = nightTotalDays;
        }

        #region Initialize/Dispose
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(new List<string>() { "longnight" }, LongNight, "longnight", "ln") { HelpText = "永夜开关" });
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }
        #endregion

        private void OnUpdate(EventArgs args)
        {
            if (Main.dayTime)
                return;

            if(!isInit){
                isInit = true;
                Console.WriteLine("永夜模式：{0},  循环天数：{1}天", nightEnable ? "已开启":"已关闭", nightTotalDays);
            }
            
            if(!nightEnable)
                return;

            // 31800    GetTime("04:20");
            // 32100    GetTime("04:20");
            // 32340    GetTime("04:29");
            double startTik = 32100;
            double endTik = 32340;
            if(  Main.time>=startTik && Main.time<=endTik )
            {
                // Console.WriteLine("startTik:{0}, endTik:{1}, currentTime:{2}", startTik, endTik, Main.time);

                if(nightRemainDays>1){
                    Console.WriteLine("永夜模式：{0},  {1}/{2}", nightEnable ? "已开启":"已关闭", nightRemainDays, nightTotalDays);
                    nightRemainDays --;
                    // bool isBloodMoon = Main.bloodMoon;

                    // 改变月相
                    int moon =Main.moonPhase >=7 ? 0: Main.moonPhase+1;
                    Main.moonPhase = moon;
                    Console.WriteLine("月相：{0}", moon);

                    // 重置时间到晚上
                    // TSPlayer.Server.SetTime(false, 16200.0);
                    TSPlayer.Server.SetTime(false, 0.0);
                    TSPlayer.All.SendInfoMessage("鵺晚仍将继续 {0}/{1}", nightRemainDays, nightTotalDays);
                } else {
                    nightRemainDays = nightTotalDays-1;
                    TSPlayer.Server.SetTime(true, 0);
                    TSPlayer.All.SendInfoMessage("现已进入白天");
                    Console.WriteLine("永夜模式：{0},  循环天数：{1}天，现已进入白天", nightEnable ? "已开启":"已关闭", nightTotalDays);
                }

            }
        }

        private void LongNight(CommandArgs args)
        {
            if (args.Parameters.Count<string>() == 0)
            {
                args.Player.SendErrorMessage("语法错误，/ln help 可查询帮助信息");
                return;
            }

            switch (args.Parameters[0].ToLowerInvariant())
            {
                default:
                    args.Player.SendErrorMessage("语法错误！，请输入 /ln help 获取帮助");
                    break;

                case "help":
                    args.Player.SendInfoMessage("/ln info, 查看永夜情况，重启后永夜参数将恢复至默认状态");
                    args.Player.SendInfoMessage("/ln false, 关闭永夜");
                    args.Player.SendInfoMessage("/ln true, 开启永夜");
                    args.Player.SendInfoMessage("/ln total <number>, 设置永夜循环天数");
                    args.Player.SendInfoMessage("/ln remain <number>, 设置永夜剩余天数");
                    break;

                case "true":
                    nightEnable = true;
                    args.Player.SendInfoMessage("永夜模式 已开启");
                    break;

                case "false":
                    nightEnable = false;
                    args.Player.SendInfoMessage("永夜模式 已关闭");
                    break;

                case "info":
                    args.Player.SendInfoMessage("永夜模式：{0}", nightEnable ? "已开启":"已关闭");
                    args.Player.SendInfoMessage("永夜循环天数：{0}", nightTotalDays);
                    args.Player.SendInfoMessage("永夜剩余天数：{0}", nightRemainDays);
                    break;

                case "total":
                    if(args.Parameters.Count >1){
                        int days = 3;
                        if( int.TryParse(args.Parameters[1], out days)){
                            nightTotalDays = days;
                        } else{
                            args.Player.SendErrorMessage("请输入正确的天数");
                        }
                    } else {
                        args.Player.SendErrorMessage("请输入天数");
                    }
                    break;

                case "remain":
                    if(args.Parameters.Count >1){
                        int days = 3;
                        if( int.TryParse(args.Parameters[1], out days)){
                            nightRemainDays = days;
                        } else{
                            args.Player.SendErrorMessage("请输入正确的天数");
                        }
                    } else {
                        args.Player.SendErrorMessage("请输入天数");
                    }
                    break;

            }
        }

        private double GetTime(String time_str="18:30"){
            string[] array = time_str.Split(':');
            if (array.Length != 2)
            {
                Console.WriteLine("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                return -1;
            }

            int hours;
            int minutes;
            if (!int.TryParse(array[0], out hours) || hours < 0 || hours > 23
                || !int.TryParse(array[1], out minutes) || minutes < 0 || minutes > 59)
            {
                Console.WriteLine("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                return -1;
            }

            decimal time = hours + (minutes / 60.0m);
            time -= 4.50m;
            if (time < 0.00m)
                time += 24.00m;

            if (time >= 15.00m)
            {
                return (double)((time - 15.00m) * 3600.0m);
            }
            else
            {
                return (double)(time * 3600.0m);
            }
        }

        private void SetTime(String time_str="18:30")
        {
            string[] array = time_str.Split(':');
            if (array.Length != 2)
            {
                Console.WriteLine("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                return;
            }

            int hours;
            int minutes;
            if (!int.TryParse(array[0], out hours) || hours < 0 || hours > 23
                || !int.TryParse(array[1], out minutes) || minutes < 0 || minutes > 59)
            {
                Console.WriteLine("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                return;
            }

            decimal time = hours + (minutes / 60.0m);
            time -= 4.50m;
            if (time < 0.00m)
                time += 24.00m;

            if (time >= 15.00m)
            {
                TSPlayer.Server.SetTime(false, (double)((time - 15.00m) * 3600.0m));
            }
            else
            {
                TSPlayer.Server.SetTime(true, (double)(time * 3600.0m));
            }
            Console.WriteLine("{0} set the time to {1}:{2:D2}.");

        }
    }
}
