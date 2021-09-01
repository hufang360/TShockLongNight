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

        private int nightTotal = 8;
        private int nightCurrent = 0;
        private bool nightEnable = true;
        private bool isFirstUpdate = false;
        private int firstUpdateDelay = 0;

        // 今天是否已提示
        private bool isNoticed = false;

        public Plugin(Main game) : base(game)
        {
        }

        #region Initialize/Dispose
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(new List<string>() { "longnight" }, LongNight, "longnight", "ln") { HelpText = "永夜控制" });
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
            if(!isFirstUpdate){
                isFirstUpdate = true;
                firstUpdateDelay = 120;
                Console.WriteLine("永夜模式：{0},  夜晚总数：{1}天", nightEnable ? "已开启":"已关闭", nightTotal);
            }

            if (Main.dayTime)
                return;

            if(!nightEnable)
                return;


            if( isFirstUpdate && firstUpdateDelay>0 ){
                firstUpdateDelay --;
                if( firstUpdateDelay==0 ){
                    TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(nightCurrent) );
                    return;
                }
            }

            // 19:31~19:35
            double startTik = 60;
            double endTik = 300;
            if(  nightCurrent==0 && !isNoticed && Main.time>=startTik && Main.time<=endTik ){
                isNoticed = true;
                firstUpdateDelay = 0;
                TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(nightCurrent) );
                return;
            }


            // 31800    GetTime("04:20");
            // 32100    GetTime("04:25");
            // 32340    GetTime("04:29");
            // 16200    午夜
            startTik = 32100;
            endTik = 32340;
            if(  Main.time>=startTik && Main.time<=endTik )
            {
                if(nightCurrent<nightTotal-1){
                    nightCurrent ++;

                    // 改变月相
                    int moon = Main.moonPhase >=7 ? 0: Main.moonPhase+1;
                    Main.moonPhase = moon;

                    // 改变渔夫任务
                    Main.AnglerQuestSwap();

                    // 重置时间到晚上
                    TSPlayer.Server.SetTime(false, 0.0);
                    TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(nightCurrent) );
                    Console.WriteLine("永夜模式：{0},  夜晚总数：{1}天", nightEnable ? "已开启":"已关闭", nightTotal);
                } else {
                    nightCurrent = 0;
                    isNoticed = false;
                    TSPlayer.Server.SetTime(true, 0);
                    TSPlayer.All.SendInfoMessage("『 永夜 · 终章 』");
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

            int days = 0;
            switch (args.Parameters[0].ToLowerInvariant())
            {
                default:
                    args.Player.SendErrorMessage("语法错误！，请输入 /ln help 获取帮助");
                    break;

                case "help":
                    args.Player.SendInfoMessage("/ln info, 查看永夜情况");
                    args.Player.SendInfoMessage("/ln false, 关闭永夜");
                    args.Player.SendInfoMessage("/ln true, 开启永夜");
                    args.Player.SendInfoMessage("/ln total <number>, 设置永夜循环天数");
                    args.Player.SendInfoMessage("/ln current <number>, 设置当前处于永夜的第几天");
                    break;

                case "true":
                case "0":
                    nightEnable = true;
                    args.Player.SendSuccessMessage("永夜模式 已开启");
                    break;

                case "false":
                case "1":
                    nightEnable = false;
                    args.Player.SendInfoMessage("永夜模式 已关闭");
                    break;

                case "info":
                case "i":
                    args.Player.SendInfoMessage("永夜");
                    args.Player.SendInfoMessage("模式：{0}", nightEnable ? "已开启":"已关闭");
                    args.Player.SendInfoMessage("天数：{0}/{1}（{2}）", (nightCurrent+1), nightTotal, GetZhNum(nightCurrent));

                    string msg =  GetMoon(Main.moonPhase);
                    var itemID = Main.anglerQuestItemNetIDs[Main.anglerQuest].ToString();
                    List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemID);
                    if (matchedItems.Count > 0)
                    {
                        msg += ", " + matchedItems[0].Name;
                    }
                    msg += ", " + Main.time.ToString();
                    args.Player.SendInfoMessage("附加信息：{0}",msg);
                    break;

                case "total":
                case "t":
                    if(args.Parameters.Count >1){
                        if( int.TryParse(args.Parameters[1], out days)){
                            if(days<2){
                                args.Player.SendErrorMessage("总天数不应小于2天");
                            } else {
                                nightTotal = days;
                                args.Player.SendSuccessMessage("永夜总天数 已改为 {0} 天", days);
                            }
                        } else{
                            args.Player.SendErrorMessage("请输入正确的天数");
                        }
                    } else {
                        args.Player.SendErrorMessage("请输入天数");
                    }
                    break;

                case "current":
                case "cur":
                case "c":
                    if(args.Parameters.Count >1){
                        if( int.TryParse(args.Parameters[1], out days)){
                            if(days==0){
                                args.Player.SendSuccessMessage("当前天数不能为 0");
                            } else if (days>nightTotal){
                                args.Player.SendSuccessMessage("不能超过永夜总天数{0}", nightTotal);
                            } else {
                                args.Player.SendSuccessMessage("已改为永夜第 {0} 天", days);
                                nightCurrent = days-1;
                            }
                        } else{
                            args.Player.SendErrorMessage("请输入正确的天数");
                        }
                    } else {
                        args.Player.SendErrorMessage("请输入天数");
                    }
                    break;

            }
        }

        private String  GetZhNum(int index){
            String[] arr = new string[] {"一", "二", "三", "四", "五", "六", "七", "八", "九", "十"};
            if(index==-1 || index+1>arr.Length)
                return (index+1).ToString();

            return arr[index];
        }

        private String  GetMoon(int index){
            String[] arr = new string[] {"满月", "亏凸月", "下弦月", "残月", "新月", "娥眉月", "上弦月", "盈凸月"};
            if (index==-1 || index+1> arr.Length)
                return "未知";

            return arr[index];
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
