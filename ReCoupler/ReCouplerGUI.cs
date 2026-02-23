using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens;

namespace ReCoupler
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class ReCouplerGUI : MonoBehaviour
    {
        public static ReCouplerGUI Instance;

        public List<Part[]> partPairsToIgnore = new List<Part[]>();
        public List<Color> colorSpectrum = new List<Color> { Color.red, Color.yellow, Color.cyan, Color.blue, Color.magenta, Color.white };
        public bool GUIVisible = false;
        public bool HighlightOn
        {
            get
            {
                return _highlightOn;
            }
            set
            {
                if (_highlightOn == value)
                    return;
                else
                    _highlightOn = value;
            }
        }
        
        private bool _highlightOn = false;
        private bool _highlightWasOn = false;
        private bool selectActive = false;
        private bool inputLocked = false;
        private const string iconPath = "ReCoupler/ReCoupler_Icon";
        private const string iconPath_off = "ReCoupler/ReCoupler_Icon_off";
        private const string iconPath_blizzy = "ReCoupler/ReCoupler_blizzy_Icon";
        private const string iconPath_blizzy_off = "ReCoupler/ReCoupler_blizzy_Icon_off";
        private string connectRadius_string = ReCouplerSettings.connectRadius_default.ToString();
        private string connectAngle_string = ReCouplerSettings.connectAngle_default.ToString();
        private bool allowRoboJoints_bool = ReCouplerSettings.allowRoboJoints_default;
        private bool allowKASJoints_bool = ReCouplerSettings.allowKASJoints_default;
        protected Vector2 ReCouplerWindow = new Vector2(-1, -1);
        internal protected List<AbstractJointTracker> jointsInvolved = null;
        public bool appLauncherEventSet = false;
        private readonly List<Part> highlightedParts = new List<Part>();

        private static ApplicationLauncherButton button = null;
        internal static IButton blizzyToolbarButton = null;

        private PopupDialog dialog = null;

        private static readonly Logger log = new Logger("ReCouplerGui: ");

        public void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;

            if (!ActivateBlizzyToolBar())
            {
                //log.debug("Registering GameEvents.");
                appLauncherEventSet = true;
                GameEvents.onGUIApplicationLauncherReady.Add(OnGuiApplicationLauncherReady);
            }
            InputLockManager.RemoveControlLock("ReCoupler_EditorLock");
        }

        private void OnGuiApplicationLauncherReady()
        {
            button = ApplicationLauncher.Instance.AddModApplication(
                OnTrue,
                OnFalse,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.FLIGHT,
                GameDatabase.Instance.GetTexture(iconPath, false));
        }

        internal bool ActivateBlizzyToolBar()
        {
            try
            {
                if (!ToolbarManager.ToolbarAvailable) return false;
                if (HighLogic.LoadedScene != GameScenes.EDITOR && HighLogic.LoadedScene != GameScenes.FLIGHT) return true;
                blizzyToolbarButton = ToolbarManager.Instance.add("ReCoupler", "ReCoupler");
                blizzyToolbarButton.TexturePath = iconPath_blizzy;
                blizzyToolbarButton.ToolTip = Localizer.Format("#autoLOC_RCP000");  // "ReCoupler"
                blizzyToolbarButton.Visible = true;
                blizzyToolbarButton.OnClick += (e) =>
                {
                    OnButtonToggle();
                };
                return true;
            }
            catch
            {
                // Blizzy Toolbar instantiation error.  ignore.
                return false;
            }
        }

        public void OnButtonToggle()
        {
            if (!GUIVisible)
                OnTrue();
            else
                OnFalse();
        }

        public void OnTrue()
        {
            connectRadius_string = ReCouplerSettings.connectRadius.ToString();
            connectAngle_string = ReCouplerSettings.connectAngle.ToString();
            allowRoboJoints_bool = ReCouplerSettings.allowRoboJoints;
            allowKASJoints_bool = ReCouplerSettings.allowKASJoints;
            GUIVisible = true;

            if (ReCouplerWindow.x == -1 && ReCouplerWindow.y == -1)
            {
                ReCouplerWindow = new Vector2(0.75f, 0.5f);
            }

            dialog = SpawnPopupDialog();
            dialog.RTrf.position = ReCouplerWindow;

            button?.SetTexture(GameDatabase.Instance.GetTexture(iconPath_off, false));
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy_off;
        }
        public void OnFalse()
        {
            _highlightOn = false;
            selectActive = false;
            GUIVisible = false;
            SaveWindowPosition();
            dialog.Dismiss();
            Destroy(dialog);
            dialog = null;
            UnlockEditor();
            button?.SetTexture(GameDatabase.Instance.GetTexture(iconPath, false));
            if (blizzyToolbarButton != null)
                blizzyToolbarButton.TexturePath = iconPath_blizzy;
        }

        public void Start()
        {
            ReCouplerSettings.LoadSettings();
            this.connectRadius_string = ReCouplerSettings.connectRadius.ToString();
            this.connectAngle_string = ReCouplerSettings.connectAngle.ToString();
            this.allowRoboJoints_bool = ReCouplerSettings.allowRoboJoints;
            this.allowKASJoints_bool = ReCouplerSettings.allowKASJoints;
            if (!ReCouplerSettings.showGUI)
            {
                HighlightOn = false;
                if (button != null)
                {
                    button.SetFalse(true);
                    ApplicationLauncher.Instance.RemoveModApplication(button);
                }
                if(appLauncherEventSet)
                    GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiApplicationLauncherReady);
                blizzyToolbarButton?.Destroy();
            }
        }

        public void OnDestroy()
        {
            //log.debug("Unregistering GameEvents.");
            if (appLauncherEventSet)
                GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiApplicationLauncherReady);
            if (button != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
            blizzyToolbarButton?.Destroy();
            UnlockEditor();
        }

        public PopupDialog SpawnPopupDialog()
        {
            List<DialogGUIBase> dialogToDisplay = new List<DialogGUIBase>
            {
                // "Show Recoupled Parts"
                new DialogGUIToggleButton(()=>_highlightOn, Localizer.Format("#autoLOC_RCP001"), (value) => _highlightOn = value, -1, 30) { OptionInteractableCondition = () => !selectActive },
                // "Remove a link"
                new DialogGUIToggleButton(() => selectActive, Localizer.Format("#autoLOC_RCP002"), (value) => { selectActive = value; if (selectActive) { _highlightOn = true; LockEditor(); } UnlockEditor(); }, -1, 30),
                // "Reset Links"
                new DialogGUIButton(Localizer.Format("#autoLOC_RCP003"), () =>
                {
                   partPairsToIgnore.Clear();
                    if (HighLogic.LoadedSceneIsFlight && FlightReCoupler.Instance != null)
                    {
                        FlightReCoupler.Instance.RegenerateJoints(FlightGlobals.ActiveVessel);
                    }
                    else if (HighLogic.LoadedSceneIsEditor && EditorReCoupler.Instance != null)
                    {
                        EditorReCoupler.Instance.ResetAndRebuild();
                    }
                }, false),
                new DialogGUISpace(10),
                // "Settings:"
                new DialogGUILabel(Localizer.Format("#autoLOC_RCP004"), UISkinManager.defaultSkin.window),
                new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                    // "Acceptable join radius:"
                    new DialogGUILabel(Localizer.Format("#autoLOC_RCP005"), UISkinManager.defaultSkin.toggle, true),
                    new DialogGUITextInput(connectRadius_string, false, 5, (s) => connectRadius_string = s, 60, 25),
                    new DialogGUILabel("", 35)
                    ),
                new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                    // "Acceptable joint angle:"
                    new DialogGUILabel(Localizer.Format("#autoLOC_RCP006"), UISkinManager.defaultSkin.toggle, true),
                    new DialogGUITextInput(connectAngle_string, false, 5, (s) => connectAngle_string = s, 60, 25),
                    new DialogGUILabel("", 35)
                    ),
                // "Allow Breaking Ground joints between ReCoupler joints"
                new DialogGUIToggle(allowRoboJoints_bool, Localizer.Format("#autoLOC_RCP007"), (value) => allowRoboJoints_bool = value),
                // "Allow KAS joints between ReCoupler joints"
                new DialogGUIToggle(allowKASJoints_bool, Localizer.Format("#autoLOC_RCP008"), (value) => allowKASJoints_bool = value),
                // "Apply"
                new DialogGUIButton(Localizer.Format("#autoLOC_RCP009"), () =>
                {
                    if (float.TryParse(connectRadius_string, out float connectRadius_set))
                        ReCouplerSettings.connectRadius = connectRadius_set;
                    if (float.TryParse(connectAngle_string, out float connectAngle_set))
                        ReCouplerSettings.connectAngle = connectAngle_set;
                    ReCouplerSettings.allowRoboJoints = allowRoboJoints_bool;
                    ReCouplerSettings.allowKASJoints = allowKASJoints_bool;
                    if (HighLogic.LoadedSceneIsEditor && EditorReCoupler.Instance != null)
                        EditorReCoupler.Instance.ResetAndRebuild();
                    else if (HighLogic.LoadedSceneIsFlight && FlightReCoupler.Instance != null)
                        FlightReCoupler.Instance.RegenerateJoints(FlightGlobals.ActiveVessel);
                }, false),
                // "Close"
                new DialogGUIButton(Localizer.Format("#autoLOC_RCP010"), () =>
                {
                    if (button != null)
                        button.SetFalse(true);
                    else
                        OnFalse();
                })
            };

            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.StartsWith("KAS,")))
                dialogToDisplay.RemoveAt(8);
            if (!Expansions.ExpansionsLoader.IsExpansionInstalled("Serenity"))
                dialogToDisplay.RemoveAt(7);

            PopupDialog dialog = PopupDialog.SpawnPopupDialog(new Vector2(0, 1), new Vector2(0, 1),
                // "ReCoupler"
                new MultiOptionDialog(Localizer.Format("#autoLOC_RCP000"), "", Localizer.Format("#autoLOC_RCP000"), UISkinManager.defaultSkin, new Rect(ReCouplerWindow.x, ReCouplerWindow.y, 250, 150),
                    dialogToDisplay.ToArray()),
                false, UISkinManager.defaultSkin, false);
            dialog.OnDismiss += SaveWindowPosition;
            return dialog;
        }

        private void SaveWindowPosition()
        {
            ReCouplerWindow = new Vector2(dialog.RTrf.position.x / Screen.width + 0.5f, dialog.RTrf.position.y / Screen.height + 0.5f);
        }

        public void HighlightPart(Part part, int colorIndx = 0)
        {
            if (_highlightOn)
            {
                part.SetHighlightType(Part.HighlightType.AlwaysOn);
                part.SetHighlightColor(colorSpectrum[colorIndx % colorSpectrum.Count]);
                part.SetHighlight(true, false);
                if (!highlightedParts.Contains(part))
                    highlightedParts.Add(part);
            }
            else
            {
                part.SetHighlightDefault();
                highlightedParts.Remove(part);
            }
        }

        public void ResetHighlighting(List<Part> parts)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                parts[i].SetHighlightDefault();
                highlightedParts.Remove(parts[i]);
            }
        }

        public void Update()
        {
            if (_highlightOn || _highlightWasOn || selectActive)
            {
                if (HighLogic.LoadedSceneIsEditor && EditorReCoupler.Instance != null)
                    jointsInvolved = EditorReCoupler.Instance.hiddenNodes.CastList<AbstractJointTracker,EditorReCoupler.EditorJointTracker>();
                else if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null && FlightReCoupler.Instance != null)
                {
                    if (FlightReCoupler.Instance.trackedJoints.ContainsKey(FlightGlobals.ActiveVessel))
                        jointsInvolved = FlightReCoupler.Instance.trackedJoints[FlightGlobals.ActiveVessel].CastList<AbstractJointTracker,FlightReCoupler.FlightJointTracker>();
                    else
                    {
                        jointsInvolved = null;
                        log.debug("ActiveVessel is not in the dictionary!");
                    }
                }
                else
                {
                    jointsInvolved = null;
                    log.error("Could not get active joints!");
                    return;
                }
            }
            if (jointsInvolved == null)
            {
                ResetHighlighting(highlightedParts);
                return;
            }
            if (highlightedParts.Count > 0)
                ResetHighlighting(highlightedParts.FindAll(part => jointsInvolved.All(jt => !jt.parts.Contains(part))));

            if (_highlightOn || _highlightWasOn)
            {
                for (int i = 0; i < jointsInvolved.Count; i++)
                {
                    for (int j = jointsInvolved[i].parts.Count - 1; j >= 0; j--)
                    {
                        HighlightPart(jointsInvolved[i].parts[j], i);
                    }
                }
                _highlightWasOn = _highlightOn;
            }
            if (selectActive)
            {
                LockEditor();
                // "Select a part in the ReCoupler joint for removal with ctrl + left mouseclick"
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_RCP011"), 0.3f, ScreenMessageStyle.UPPER_CENTER);
                if (Input.GetKeyUp(KeyCode.Mouse0) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        Part hitPart = Part.FromGO(hit.transform.gameObject) ?? hit.transform.gameObject.GetComponentInParent<Part>();
#pragma warning disable IDE0270 // Use coalesce expression
                        if (hitPart == null)
                            hitPart = hit.collider.gameObject.GetComponentUpwards<Part>();
                        if (hitPart == null)
                            hitPart = SelectPartUnderMouse();
#pragma warning restore IDE0270 // Use coalesce expression

                        if (hitPart != null)
                        {
                            log.debug("Raycast hit part " + hitPart.name);
                            List<AbstractJointTracker> hitJoints = jointsInvolved.FindAll(j => j.parts.Contains(hitPart));
                            for (int i = hitJoints.Count - 1; i >= 0; i--)
                            {
                                log.debug("Destroying link between " + hitJoints[i].parts[0].name + " and " + hitJoints[i].parts[1].name);
                                hitJoints[i].Destroy();
                                partPairsToIgnore.Add(hitJoints[i].parts.ToArray());
                                if (HighLogic.LoadedSceneIsEditor && EditorReCoupler.Instance != null)
                                {
                                    EditorReCoupler.Instance.hiddenNodes.Remove((EditorReCoupler.EditorJointTracker)hitJoints[i]);
                                }
                                else if (HighLogic.LoadedSceneIsFlight && FlightReCoupler.Instance != null && FlightReCoupler.Instance.trackedJoints.ContainsKey(FlightGlobals.ActiveVessel))
                                {
                                    FlightReCoupler.Instance.trackedJoints[FlightGlobals.ActiveVessel].Remove((FlightReCoupler.FlightJointTracker)hitJoints[i]);
                                }
                                hitJoints[i].parts[0].SetHighlightDefault();
                                hitJoints[i].parts[1].SetHighlightDefault();
                            }

                            UnlockEditor();
                            selectActive = false;
                        }
                        else
                            log.debug("Hit part was null: ");
                    }
                }
            }
        }

        private void LockEditor()
        {
            if (inputLocked)
                return;
            if (!HighLogic.LoadedSceneIsEditor)
                return;
            inputLocked = true;
            //EditorLogic.fetch.Lock(false, false, false, "ReCoupler_EditorLock");
            InputLockManager.SetControlLock(ControlTypes.EDITOR_SOFT_LOCK, "ReCoupler_EditorLock");
            log.debug("Locking editor");
        }

        private void UnlockEditor()
        {
            if (!inputLocked)
                return;
            //EditorLogic.fetch.Unlock("ReCoupler_EditorLock");
            InputLockManager.RemoveControlLock("ReCoupler_EditorLock");
            inputLocked = false;
            log.debug("Unlocking editor");
        }

        public Part SelectPartUnderMouse()
        {
            log.debug("Using failsafe part select method.");
            FlightCamera CamTest = new FlightCamera();
            CamTest = FlightCamera.fetch;
            Ray ray = CamTest.mainCamera.ScreenPointToRay(Input.mousePosition);
            LayerMask RayMask = new LayerMask();
            RayMask = 1 << 0;
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, RayMask))
            {

                return FlightGlobals.ActiveVessel.Parts.Find(p => p.gameObject == hit.transform.gameObject);
                //The critical bit. Note I'm generating a list of possible objects hit and then asking if I hit one of them. I'm not starting with the object hit and trying to work my way up.
            }
            return null;
        }
    }
}
