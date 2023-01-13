using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public static class SEUtils
        {
            public static IMyProgrammableBlock CurrentProgrammableBlock;
            public static IMyCubeGrid CurrentCubeGrid;
            private static IMyGridTerminalSystem GridTerminalSystem;
            private static MyGridProgram MyGridProgram;
            private static List<Action> invokeNextUpdateActions;
            private static List<WaitingInvokeInfo> invokeTimeActions;
            private static char[] icons = new char[] { '|', '/', '-', '\\' };
            private static int iconIndex = 0;
            private static DateTime start;
            private static IMyTextSurface pbLcd;
            private static string name = "";
            private static bool setupDone = false;
            private static Dictionary<int, IEnumerator<ICoroutineInfo>> coroutines;
            private static Dictionary<int, Func<bool>> waitingCoroutines;
            private static bool statusDisplay = true;
            private static int coroutineCounter = 0;

            /// <summary>
            /// Sets SEUtils up for usage and sets the UpdateFrequency to Update10
            /// </summary>
            /// <param name="scriptBaseClass"></param>
            /// <param name="statusDisplay"></param>
            /// <param name="scriptName"></param>
            public static void Setup(MyGridProgram scriptBaseClass, bool statusDisplay = true, string scriptName = "Script")
            {
                SEUtils.statusDisplay = statusDisplay;
                coroutines = new Dictionary<int, IEnumerator<ICoroutineInfo>>();
                waitingCoroutines = new Dictionary<int, Func<bool>>();
                invokeNextUpdateActions = new List<Action>();
                invokeTimeActions = new List<WaitingInvokeInfo>();
                MyGridProgram = scriptBaseClass;
                GridTerminalSystem = MyGridProgram.GridTerminalSystem;
                CurrentProgrammableBlock = MyGridProgram.Me;
                CurrentCubeGrid = CurrentProgrammableBlock.CubeGrid;

                name = scriptName;

                start = DateTime.Now;
                int time = 0;
                if (int.TryParse(CurrentProgrammableBlock.CustomData, out time))
                    start = start.AddMilliseconds(-time);

                MyGridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update10;
                setupDone = true;

                if (statusDisplay)
                {
                    pbLcd = CurrentProgrammableBlock.GetSurface(0);
                    pbLcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    UpdatePBScreen();
                }
            }

            private static void CheckSetup()
            {
                if (!setupDone)
                {
                    throw new Exception("Please execute the 'SEUtils.Setup' method in the script constructor");
                }
            }

            private static void UpdatePBScreen()
            {
                string uptimeDisplay = "Uptime: " + (DateTime.Now - start).ToString();
                iconIndex++;
                if (iconIndex > icons.Length - 1)
                {
                    iconIndex = 0;
                }
                pbLcd.WriteText(name + (!name.ToLower().Contains("script") ? " script" : "") + " is running " + icons[iconIndex] + "\n" + uptimeDisplay);
                InvokeTime(UpdatePBScreen, 1000);
            }

            private static void CoroutineStep(int enumeratorId)
            {
                var enumerator = coroutines[enumeratorId];
                if (!coroutines.ContainsValue(enumerator))
                {
                    return;
                }
                if (enumerator.MoveNext())
                {
                    var waitInstruction = enumerator.Current;
                    MyGridProgram.Echo(waitInstruction.GetType().FullName);
                    if (waitInstruction is WaitForNextTick)
                    {
                        InvokeNextUpdate(() => CoroutineStep(enumeratorId));
                    }
                    else if (waitInstruction is WaitForMilliseconds)
                    {
                        var milliseconds = (waitInstruction as WaitForMilliseconds).milliseconds;
                        InvokeTime(() => CoroutineStep(enumeratorId), milliseconds);
                    }
                    else if (waitInstruction is WaitForConditionMet)
                    {
                        if ((waitInstruction as WaitForConditionMet).condition())
                        {
                            InvokeNextUpdate(() => CoroutineStep(enumeratorId));
                        }
                        else
                        {
                            waitingCoroutines.Add(enumeratorId, (waitInstruction as WaitForConditionMet).condition);
                        }
                    }
                    else
                    {
                        MyGridProgram.Echo("ERROR");
                    }
                }
                else
                {
                    coroutines.Remove(enumeratorId);
                }
            }

            public static int StartCoroutine(IEnumerator<ICoroutineInfo> enumerator)
            {
                int id = coroutineCounter;
                coroutineCounter++;
                coroutines.Add(id, enumerator);
                CoroutineStep(id);
                return id;
            }

            public static void StopCoroutine(int enumeratorId)
            {
                coroutines.Remove(enumeratorId);
                waitingCoroutines.Remove(enumeratorId);
            }

            /// <summary>
            /// Checks if the given block is in the grid of the PB
            /// </summary>
            /// <param name="block"></param>
            /// <returns></returns>
            public static bool IsInGrid(IMyTerminalBlock block)
            {
                CheckSetup();
                return block.CubeGrid == CurrentCubeGrid;
            }

            /// <summary>
            /// Invokes the given Action for the next tick
            /// </summary>
            /// <param name="invoke"></param>
            public static void InvokeNextUpdate(Action invoke)
            {
                CheckSetup();
                invokeNextUpdateActions.Add(invoke);
            }

            /// <summary>
            /// Invokes the given action to be executed after the given milliseconds passed
            /// </summary>
            /// <param name="invoke"></param>
            /// <param name="milliseconds"></param>
            public static void InvokeTime(Action invoke, int milliseconds)
            {
                CheckSetup();
                invokeTimeActions.Add(new WaitingInvokeInfo(invoke, DateTime.Now.AddMilliseconds(milliseconds)));
            }

            /// <summary>
            /// Execute this method in your main method at the start
            /// </summary>
            /// <param name="argument"></param>
            /// <param name="updateSource"></param>
            public static void RuntimeUpdate(string argument, UpdateType updateSource)
            {
                CheckSetup();
                var wc = waitingCoroutines.ToDictionary(x => x.Key, y => y.Value);
                foreach (var item in wc)
                {
                    if (item.Value())
                    {
                        waitingCoroutines.Remove(item.Key);
                        CoroutineStep(item.Key);
                    }
                }
                if (updateSource == UpdateType.Update1 || updateSource == UpdateType.Update10 || updateSource == UpdateType.Update100)
                {
                    var nextUp = invokeNextUpdateActions.ToArray();
                    invokeNextUpdateActions.Clear();
                    foreach (var item in nextUp)
                    {
                        item();
                    }

                    var actions = invokeTimeActions.Where(x => DateTime.Now >= x.datetime).Select(x => x.action).ToList();
                    foreach (var item in actions)
                    {
                        item();
                    }
                    invokeTimeActions = invokeTimeActions.Where(x => DateTime.Now < x.datetime).ToList();
                }
                else if (argument == "")
                {
                    start = DateTime.Now;
                }
            }
        }

        private class WaitingInvokeInfo
        {
            public Action action;
            public DateTime datetime;

            public WaitingInvokeInfo(Action action, DateTime datetime)
            {
                this.action = action;
                this.datetime = datetime;
            }
        }

        /// <summary>
        /// Use this interface for coroutines
        /// </summary>
        public interface ICoroutineInfo
        {

        }

        /// <summary>
        /// Waits for the next tick and continues then
        /// </summary>
        public class WaitForNextTick : ICoroutineInfo
        {
            public WaitForNextTick()
            {

            }
        }

        /// <summary>
        /// Waits for the given milliseconds to pass
        /// </summary>
        public class WaitForMilliseconds : ICoroutineInfo
        {
            public int milliseconds;

            public WaitForMilliseconds(int milliseconds)
            {
                this.milliseconds = milliseconds;
            }
        }

        /// <summary>
        /// Waits at least for the next tick, but waits for the condition to be true
        /// </summary>
        public class WaitForConditionMet : ICoroutineInfo
        {
            public Func<bool> condition;

            public WaitForConditionMet(Func<bool> action)
            {
                condition = action;
            }
        }
    }
}
