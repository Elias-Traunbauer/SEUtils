using Sandbox.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class SEUtils
        {
            #region Public Properties
            /// <summary>
            /// The PB that is executing this script
            /// </summary>
            public IMyProgrammableBlock CurrentProgrammableBlock;
            /// <summary>
            /// The CubeGrid the CurrentProgrammableBlock is located in
            /// </summary>
            public IMyCubeGrid CurrentCubeGrid;
            #endregion

            #region Private Properties
            private IMyGridTerminalSystem GridTerminalSystem;
            private MyGridProgram CurrentMyGridProgram;
            private List<Action> invokeNextUpdateActions;
            private List<WaitingInvokeInfo> invokeTimeActions;
            private char[] icons = new char[] { '|', '/', '-', '\\' };
            private int iconIndex = 0;
            private DateTime start;
            private IMyTextSurface pbLcd;
            private string name = "";
            private bool setupDone = false;
            private Dictionary<int, IEnumerator> coroutines;
            private int coroutineCounter = 0;
            private UpdateFrequency updateFreq;
            #endregion

            #region Setup
            /// <summary>
            /// Performs the setup, that is required for SEUtils to work
            /// </summary>
            /// <param name="scriptBaseClass">Your script class</param>
            /// <param name="updateFrequency">Your desired update frequency</param>
            /// <param name="statusDisplay">Whether SEUtils should display a simple status on the PB's screen</param>
            /// <param name="scriptName">The name of your script</param>
            public SEUtils(MyGridProgram scriptBaseClass, UpdateFrequency updateFrequency = UpdateFrequency.Update10, bool statusDisplay = true, string scriptName = "Script")
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
            private void CheckSetup()
            {
                if (!setupDone)
                {
                    throw new Exception("Please execute the 'SEUtils.Setup' method in the script constructor");
                }
            }

            private void UpdatePBScreen()
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
            private void WaitingCoroutineStep(WaitForConditionMet conditionChecker, int enumeratorId)
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
                            if (conditionChecker.timeoutAction())
                            {
                                InvokeNextTick(() => CoroutineStep(enumeratorId));
                            }
                            else
                            {
                                StopCoroutine(enumeratorId);
                            }
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

            private void CoroutineStep(int enumeratorId)
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
            public int StartCoroutine(IEnumerator coroutine)
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
            public bool StopCoroutine(int coroutineInstanceId)
            {
                return coroutines.Remove(coroutineInstanceId);
            }

            /// <summary>
            /// Checks wheter the coroutine with the given id is running
            /// </summary>
            /// <param name="coroutineInstanceId">The coroutine to check</param>
            /// <returns>if the instance was found</returns>
            public bool CheckCoroutineRunning(int coroutineInstanceId)
            {
                return coroutines.ContainsKey(coroutineInstanceId);
            }

            #endregion

            #region Util Methods
            /// <summary>
            /// Checks if the given block is on the same grid as the current PB
            /// </summary>
            /// <param name="block">Block to check the grid on</param>
            /// <returns>if the block is on the same grid as the current PB</returns>
            public bool IsInGrid(IMyTerminalBlock block)
            {
                CheckSetup();
                return block.CubeGrid == CurrentCubeGrid;
            }

            /// <summary>
            /// Invokes the given action to be executed in the next game tick
            /// </summary>
            /// <param name="action">Action to invoke</param>
            public void InvokeNextTick(Action action)
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
            public void Invoke(Action action, int milliseconds)
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
            public bool RuntimeUpdate(string argument, UpdateType updateSource)
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
        /// If timeout is specified, after the timeout passed and there is a timeoutAction specified, the timeoutAction will be executed, if timeoutAction returns true, coroutine continues, otherwise terminates
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
            /// <param name="timeoutFunc">Func to execute when timeout passed; If null, coroutine continues after timeout; If func returns true, coroutine continues, otherwise terminates</param>
            public WaitForConditionMet(Func<bool> action, int timeoutMilliseconds = -1, int checkIntervalMilliseconds = -1, Func<bool> timeoutFunc = null)
            {
                condition = action;
                timeout = timeoutMilliseconds;
                checkInterval = checkIntervalMilliseconds;
                this.timeoutAction = timeoutFunc;
                started = DateTime.Now;
            }

            public Func<bool> timeoutAction { get; set; }
            public DateTime started { get; set; }
            public int checkInterval { get; set; }
            public int timeout { get; set; }
        }
        #endregion

        #region LCD Projection
        /// <summary>
        /// Class to project points from a world position onto an lcd
        /// NOTE: Only works if the ViewPoint is infront of the lcd -> Transparent LCDS from the back dont work
        /// </summary>
        public class TextPanelRenderingContext
        {
            public Vector3D ViewPoint { get; private set; }
            public IMyTextPanel TextPanel { get; private set; }
            public Vector2 PixelMultiplier { get; private set; }

            private readonly Vector3D Normal = Vector3D.Backward;
            public static readonly double TextPanelThickness = 0.05f;
            public static readonly double D = 2.5d / 2d - TextPanelThickness;
            public static float TextPanelTextureMargin = 0f;

            /// <summary>
            /// Initializes the renderer to a working state
            /// </summary>
            /// <param name="lcd">The lcd you want to project to</param>
            /// <param name="viewPointDirection">Direction to view from local to lcd's matrix</param>
            public TextPanelRenderingContext(ref IMyTextPanel lcd, Vector3D viewPointDirection)
            {
                TextPanel = lcd;
                ViewPoint = viewPointDirection;
                // magic numbers for lcd margin
                TextPanelTextureMargin = TextPanel.BlockDefinition.SubtypeId == "TransparentLCDLarge" ? 0.33f : -0.08f;
                var screenSize = GetTextPanelSizeFromGridView(TextPanel);
                PixelMultiplier = TextPanel.TextureSize / ((Vector2)screenSize * (2.5f - TextPanelTextureMargin));
                float maxMult = PixelMultiplier.X > PixelMultiplier.Y ? PixelMultiplier.Y : PixelMultiplier.X;
                PixelMultiplier = new Vector2(maxMult, maxMult);
            }

            private static Vector2I GetTextPanelSizeFromGridView(IMyTextPanel textPanel)
            {
                Vector3I lcdSize = textPanel.Max - textPanel.Min;
                Vector2I screenSize = new Vector2I();
                switch (textPanel.Orientation.Forward)
                {
                    case Base6Directions.Direction.Forward:
                        screenSize = new Vector2I(lcdSize.Y, lcdSize.X);
                        break;
                    case Base6Directions.Direction.Backward:
                        screenSize = new Vector2I(lcdSize.Y, lcdSize.X);
                        break;
                    case Base6Directions.Direction.Left:
                        screenSize = new Vector2I(lcdSize.Z, lcdSize.Y);
                        break;
                    case Base6Directions.Direction.Right:
                        screenSize = new Vector2I(lcdSize.Z, lcdSize.Y);
                        break;
                    case Base6Directions.Direction.Up:
                        screenSize = new Vector2I(lcdSize.X, lcdSize.Z);
                        break;
                    case Base6Directions.Direction.Down:
                        screenSize = new Vector2I(lcdSize.X, lcdSize.Z);
                        break;
                    default:
                        throw new ArgumentException("Unknown orientation");
                }
                screenSize += new Vector2I(1, 1);
                return screenSize;
            }

            /// <summary>
            /// Projects the given point onto LCD screen coordinates given in pixels
            /// </summary>
            /// <param name="worldPoint">The point to project</param>
            /// <returns>Screen coordinate in pixels or null if projection is not on lcd</returns>
            public Vector2? ProjectPoint(Vector3D worldPoint)
            {
                Vector3D referenceWorldPosition = TextPanel.WorldMatrix.Translation;
                // Get world direction
                Vector3D worldDirection = worldPoint - Vector3D.Transform(ViewPoint, TextPanel.WorldMatrix);
                // Convert worldDirection into a local direction
                Vector3D localRayDirection = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(TextPanel.WorldMatrix));

                // project the plane onto the plane
                Vector2? projectedLocalPoint = PlaneIntersection(ViewPoint, localRayDirection);
                if (projectedLocalPoint != null)
                {
                    var projectedLocalPointNonNullable = (Vector2)projectedLocalPoint;
                    // convert it to pixels
                    Vector2 projectedLocalPointPixels = projectedLocalPointNonNullable * PixelMultiplier * new Vector2(1, -1) + TextPanel.TextureSize / 2f;

                    if (projectedLocalPointPixels.X >= 0 && projectedLocalPointPixels.Y >= 0 && projectedLocalPointPixels.X < TextPanel.SurfaceSize.X && projectedLocalPointPixels.Y < TextPanel.SurfaceSize.Y)
                    {
                        return projectedLocalPointPixels;
                    }
                }
                return null;
            }

            /// <summary>
            /// Calculates the intersection point from the given line and a plane with origin (0,0,0) and the normal (static)
            /// </summary>
            /// <param name="origin">Line origin</param>
            /// <param name="dir">Line direction</param>
            /// <returns>The projected point</returns>
            private Vector2? PlaneIntersection(Vector3D origin, Vector3D dir)
            {
                if (dir.Z >= 0)
                {
                    return null;
                }
                var t = -(Vector3D.Dot(origin, Normal) + D) / Vector3D.Dot(dir, Normal);
                Vector3D res = origin + t * dir;
                return new Vector2((float)res.X, (float)res.Y);
            }
        }
        #endregion
    }
}
