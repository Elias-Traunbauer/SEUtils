﻿using Sandbox.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program
    {
        public static class SEUtils
        {
            #region Public Properties
            /// <summary>
            /// The PB that is executing this script
            /// </summary>
            public static IMyProgrammableBlock CurrentProgrammableBlock;
            /// <summary>
            /// The CubeGrid the CurrentProgrammableBlock is located in
            /// </summary>
            public static IMyCubeGrid CurrentCubeGrid;
            #endregion

            #region Private Properties
            private static IMyGridTerminalSystem GridTerminalSystem;
            private static MyGridProgram CurrentMyGridProgram;
            private static List<Action> invokeNextUpdateActions;
            private static List<WaitingInvokeInfo> invokeTimeActions;
            private static char[] icons = new char[] { '|', '/', '-', '\\' };
            private static int iconIndex = 0;
            private static DateTime start;
            private static IMyTextSurface pbLcd;
            private static string name = "";
            private static bool setupDone = false;
            private static Dictionary<int, IEnumerator> coroutines;
            private static int coroutineCounter = 0;
            private static UpdateFrequency updateFreq;
            #endregion

            #region Setup
            /// <summary>
            /// Performs the setup, that is required for SEUtils to work
            /// </summary>
            /// <param name="scriptBaseClass">Your script class</param>
            /// <param name="updateFrequency">Your desired update frequency</param>
            /// <param name="statusDisplay">Whether SEUtils should display a simple status on the PB's screen</param>
            /// <param name="scriptName">The name of your script</param>
            public static void Setup(MyGridProgram scriptBaseClass, UpdateFrequency updateFrequency = UpdateFrequency.Update10, bool statusDisplay = true, string scriptName = "Script")
            {
                updateFreq = updateFrequency;
                coroutines = new Dictionary<int, IEnumerator>();
                invokeNextUpdateActions = new List<Action>();
                invokeTimeActions = new List<WaitingInvokeInfo>();
                CurrentMyGridProgram = scriptBaseClass;
                GridTerminalSystem = CurrentMyGridProgram.GridTerminalSystem;
                CurrentProgrammableBlock = CurrentMyGridProgram.Me;
                CurrentCubeGrid = CurrentProgrammableBlock.CubeGrid;

                name = scriptName;

                CurrentMyGridProgram.Runtime.UpdateFrequency = updateFreq;
                setupDone = true;

                if (statusDisplay)
                {
                    pbLcd = CurrentProgrammableBlock.GetSurface(0);
                    pbLcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    UpdatePBScreen();
                }

                start = DateTime.Now;
            }
            #endregion

            #region Private Setup and Status methods
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
                Invoke(UpdatePBScreen, 1000);
            }
            #endregion

            #region Coroutine Private Methods
            private static void WaitingCoroutineStep(WaitForConditionMet conditionChecker, int enumeratorId)
            {
                if (!coroutines.ContainsKey(enumeratorId))
                {
                    return;
                }
                var enumerator = coroutines[enumeratorId];
                if (conditionChecker.condition())
                {
                    CoroutineStep(enumeratorId);
                }
                else
                {
                    if (conditionChecker.timeout != -1 && (DateTime.Now - conditionChecker.started).TotalMilliseconds >= conditionChecker.timeout)
                    {
                        if (conditionChecker.timeoutAction != null)
                        {
                            conditionChecker.timeoutAction();
                            StopCoroutine(enumeratorId);
                        }
                        else
                        {
                            InvokeNextTick(() => CoroutineStep(enumeratorId));
                        }
                    }
                    if (conditionChecker.checkInterval == -1)
                    {
                        InvokeNextTick(() => WaitingCoroutineStep(conditionChecker, enumeratorId));
                    }
                    else
                    {
                        Invoke(() => WaitingCoroutineStep(conditionChecker, enumeratorId), conditionChecker.checkInterval);
                    }
                }
            }

            private static void CoroutineStep(int enumeratorId)
            {
                if (!coroutines.ContainsKey(enumeratorId))
                {
                    return;
                }
                var enumerator = coroutines[enumeratorId];
                if (enumerator.MoveNext())
                {
                    var waitInstruction = enumerator.Current;

                    if (waitInstruction is WaitForNextTick)
                    {
                        InvokeNextTick(() => CoroutineStep(enumeratorId));
                    }
                    else if (waitInstruction is WaitForMilliseconds)
                    {
                        var milliseconds = (waitInstruction as WaitForMilliseconds).milliseconds;
                        Invoke(() => CoroutineStep(enumeratorId), milliseconds);
                    }
                    else if (waitInstruction is WaitForConditionMet)
                    {
                        InvokeNextTick(() => WaitingCoroutineStep(waitInstruction as WaitForConditionMet, enumeratorId));
                    }
                    else
                    {
                        CurrentMyGridProgram.Echo("Unknown coroutine waiting instruction");
                    }
                }
                else
                {
                    coroutines.Remove(enumeratorId);
                }
            }
            #endregion

            #region Coroutine Public Methods
            /// <summary>
            /// Starts the given coroutine and returns the id of the coroutine's instance
            /// </summary>
            /// <param name="coroutine">Coroutine to start</param>
            /// <returns>Id of the coroutine's instance</returns>
            public static int StartCoroutine(IEnumerator coroutine)
            {
                int id = coroutineCounter;
                coroutineCounter++;
                coroutines.Add(id, coroutine);
                CoroutineStep(id);
                return id;
            }

            /// <summary>
            /// Stops a coroutine-instance if present and returns if the instance was found and stopping was successful
            /// </summary>
            /// <param name="coroutineInstanceId">The coroutine to stop</param>
            /// <returns>if the instance was found and stopping was successful</returns>
            public static bool StopCoroutine(int coroutineInstanceId)
            {
                return coroutines.Remove(coroutineInstanceId);
            }

            #endregion

            #region Util Methods
            /// <summary>
            /// Checks if the given block is on the same grid as the current PB
            /// </summary>
            /// <param name="block">Block to check the grid on</param>
            /// <returns>if the block is on the same grid as the current PB</returns>
            public static bool IsInGrid(IMyTerminalBlock block)
            {
                CheckSetup();
                return block.CubeGrid == CurrentCubeGrid;
            }

            /// <summary>
            /// Invokes the given action to be executed in the next game tick
            /// </summary>
            /// <param name="action">Action to invoke</param>
            public static void InvokeNextTick(Action action)
            {
                CheckSetup();
                CurrentMyGridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update1 | updateFreq;
                invokeNextUpdateActions.Add(action);
            }

            /// <summary>
            /// Invokes the given action to be executed after the given milliseconds passed
            /// </summary>
            /// <param name="action">Action to execute</param>
            /// <param name="milliseconds">Milliseconds to wait</param>
            public static void Invoke(Action action, int milliseconds)
            {
                CheckSetup();
                invokeTimeActions.Add(new WaitingInvokeInfo(action, DateTime.Now.AddMilliseconds(milliseconds)));
            }
            #endregion

            #region UpdateRuntime Method
            /// <summary>
            /// It is necessary that you call this method in your Main method at the beginning.
            /// Only execute your script's code if this method returns true.
            /// </summary>
            /// <param name="argument">The parameter 'argument' that is passed to your Main method</param>
            /// <param name="updateSource">The parameter 'updateSource' that is passed to your Main method</param>
            /// <returns>If you should execute your code</returns>
            public static bool RuntimeUpdate(string argument, UpdateType updateSource)
            {
                try
                {
                    CheckSetup();
                    if (updateSource.HasFlag(UpdateType.Update1) || updateSource.HasFlag(UpdateType.Update10) || updateSource.HasFlag(UpdateType.Update100))
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
                        if (invokeTimeActions.Any(x => (DateTime.Now - x.datetime).TotalMilliseconds < 10 * (1000 / 60)))
                        {
                            CurrentMyGridProgram.Runtime.UpdateFrequency = UpdateFrequency.Update1 | updateFreq;
                        }
                    }

                    if ((updateSource.HasFlag((UpdateType)(((int)updateFreq) * 32))) || updateSource.HasFlag(UpdateType.Once) || updateSource.HasFlag(UpdateType.Trigger) || updateSource.HasFlag(UpdateType.Script) || updateSource.HasFlag(UpdateType.None)) // updateFrequency to UpdateType
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    pbLcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    pbLcd.WriteText("Exception in SEUtils caught: " + ex.Message + "\n" + ex.StackTrace);
                    throw ex;
                }
            }
            #endregion
        }

        #region Info Classes
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
        /// Waits for the next game tick and continues the coroutine
        /// </summary>
        public class WaitForNextTick
        {

        }

        /// <summary>
        /// Waits for the given milliseconds to pass
        /// </summary>
        public class WaitForMilliseconds
        {
            public int milliseconds;

            public WaitForMilliseconds(int milliseconds)
            {
                this.milliseconds = milliseconds;
            }
        }

        /// <summary>
        /// Waits at least for the next game tick, after that waits for the given condition to be true
        /// If timeout is specified, after the timeout passed and there is a timeoutAction specified, the timeoutAction will be executed, otherwise coroutine continues
        /// </summary>
        public class WaitForConditionMet
        {
            public Func<bool> condition;

            /// <summary>
            /// Waits for the given condition to be true, but waits at least for the next game tick
            /// </summary>
            /// <param name="action">Action that evaluates your condition</param>
            /// <param name="timeoutMilliseconds">Timeout; -1 for none; Coroutine continues after timeout has passed, if no timeoutAction is passed</param>
            /// <param name="checkIntervalMilliseconds">Delay to wait between the condition checks; -1 for none</param>
            /// <param name="timeoutAction">Action to execute when timeout passed; If null, coroutine continues after timeout</param>
            public WaitForConditionMet(Func<bool> action, int timeoutMilliseconds = -1, int checkIntervalMilliseconds = -1, Action timeoutAction = null)
            {
                condition = action;
                timeout = timeoutMilliseconds;
                checkInterval = checkIntervalMilliseconds;
                this.timeoutAction = timeoutAction;
                started = DateTime.Now;
            }

            public Action timeoutAction { get; set; }
            public DateTime started { get; set; }
            public int checkInterval { get; set; }
            public int timeout { get; set; }
        }
        #endregion
    }
}
