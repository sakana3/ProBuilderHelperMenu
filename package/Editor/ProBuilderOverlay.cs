using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor.EditorTools;
using UnityEditor.Toolbars;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor;
using System.Linq;
using System.Reflection;
using UnityEngine.WSA;
using System.Drawing.Imaging;
using static UnityEngine.GridBrushBase;
using static UnityEditor.PlayerSettings;
using UnityEditor.Actions;
using UnityEngine.ProBuilder;
using Codice.Client.GameUI.Checkin;

namespace UnityEditor.ProBuilder
{
    [Overlay(typeof(SceneView), "ProBuilderTools", false)]
    public class ProBuilderOverlay : Overlay , ICreateHorizontalToolbar , ICreateVerticalToolbar
    {
        protected struct MenuActionContent
        {
            public UnityEditor.ProBuilder.MenuAction action;
            public void PopulateAction()
            {
                var type = action.GetType();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                bool hasFileMenuEntry = (bool)type.GetProperty("hasFileMenuEntry", flags).GetValue(action);
                bool optionsEnabled = (bool)type.GetProperty("optionsEnabled", flags).GetValue(action);

                if (optionsEnabled)
                {
                    EditorAction.Start(new MenuActionSettings(action, true));
                    var ProBuilderAnalyticsType = System.Type.GetType("UnityEditor.ProBuilder.ProBuilderAnalytics,Unity.ProBuilder.Editor");
                    var funcs = ProBuilderAnalyticsType.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    foreach (var f in funcs)
                    {
                        if (f.GetParameters().Length == 1)
                        {
                            f.Invoke(null, new object[] { action });
                        }
                    }
                }
                else
                {
                    action.PerformAction();
                }
            }

        }
        protected List<MenuActionContent> menuActions;

        private string StringReplace( string src )
        {
            var half = src.Length / 2;

            int index = int.MaxValue;
            for ( int i = 0; i < src.Length; i ++ )
            {
                if (src[i] == ' ')
                {
                    if (Mathf.Abs(src.Length / 2 - i) < index)
                    {
                        index = i;
                    }
                }
            }
            if( index != int.MaxValue)
            {
                StringBuilder sb = new StringBuilder(src);
                sb[index] = '\n';
                return sb.ToString();
            }
            return src;
        }

        public OverlayToolbar CreateVerticalToolbarContent()
        {
            var horizontalOverlayToolbar = new OverlayToolbar();
            var root = new OverlayToolbar() { name = "ProBuilderTools" };

            AddContextMenu(root, "Menu", ToolbarGroup.Tool, ToolbarGroup.Selection, ToolbarGroup.Object, ToolbarGroup.Export);

            AddButtons(root,true);
            horizontalOverlayToolbar.Add(root);
            return horizontalOverlayToolbar;
        }


        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            var horizontalOverlayToolbar = new OverlayToolbar();
            var root = new OverlayToolbar() { name = "ProBuilderTools" };

            AddContextMenu(root, "Menu", ToolbarGroup.Tool, ToolbarGroup.Selection, ToolbarGroup.Object, ToolbarGroup.Export);

            AddButtons(root,true);
            horizontalOverlayToolbar.Add(root);
            return horizontalOverlayToolbar;
        }

        public override void OnCreated()
        {

            if (menuActions == null)
            {
                menuActions = new List<MenuActionContent>();
                var EditorToolbarLoaderType = System.Type.GetType("UnityEditor.ProBuilder.EditorToolbarLoader,Unity.ProBuilder.Editor");

                var getActionFunc = EditorToolbarLoaderType.GetMethod("GetActions", BindingFlags.Static | BindingFlags.NonPublic);

                var actions = (List<UnityEditor.ProBuilder.MenuAction>)getActionFunc.Invoke(null, new object[] { true });
                actions = actions.OrderBy(a => a.toolbarPriority).ToList();
                foreach (var action in actions)
                {
                    var tooltip = action.tooltip.summary;
                    if (string.IsNullOrEmpty(action.tooltip.shortcut) is false)
                    {
                        tooltip += $"\n{action.tooltip.shortcut}";
                    }
                    MenuActionContent content = new MenuActionContent()
                    {
                        action = action,
                    };
                    this.menuActions.Add(content);
                }

                UnityEditor.ProBuilder.ProBuilderEditor.selectionUpdated += selectionUpdated;
                UnityEditor.ProBuilder.ProBuilderEditor.selectModeChanged += selectModeChanged;
                UnityEditor.ProBuilder.ProBuilderEditor.afterMeshModification += selectionUpdated;
                UnityEditor.ProBuilder.ProBuilderEditor.beforeMeshModification += selectionUpdated;
                Selection.selectionChanged += OnChangedSelection;
            }
        }


        public void OnChangedSelection()
        {
            foreach( var go in Selection.gameObjects )
            {
                var pb = go.GetComponent<ProBuilderMesh>();
                if(pb != null )
                {
                    displayed = true;
                    return;
                }
            }
            displayed = false;
        }


        public override void OnWillBeDestroyed()
        {
            UnityEditor.ProBuilder.ProBuilderEditor.selectionUpdated -= selectionUpdated;
            UnityEditor.ProBuilder.ProBuilderEditor.selectModeChanged -= selectModeChanged;
            UnityEditor.ProBuilder.ProBuilderEditor.afterMeshModification -= selectionUpdated;
            UnityEditor.ProBuilder.ProBuilderEditor.beforeMeshModification -= selectionUpdated;
            Selection.selectionChanged -= OnChangedSelection;
        }

        private void selectionUpdated( IEnumerable<ProBuilderMesh> _ )
        {
            Refresh();
        }

        private void selectModeChanged( SelectMode _ )
        {
            Refresh();
        }


        private void Refresh()
        {
            if(displayed)
            {
                foreach (var button in this.buttons)
                {
                    button.button.enabledSelf = button.action.enabled;
                }
            }
        }

        List<(Button button ,MenuAction action)> buttons;

        private readonly List<GameObject> _list = new();
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement() { name = "ProBuilderTools" };
            root.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);

            AddContextMenu(root, "Tool Menu", ToolbarGroup.Tool);
            AddContextMenu(root, "Select Menu", ToolbarGroup.Selection);
            AddContextMenu(root, "Object Menu", ToolbarGroup.Object);
            AddContextMenu(root, "Export Menu", ToolbarGroup.Object);

            root.Add(new Label("Geometry"));

            AddButtons(root,false);
            return root;
        }


        private void AddButtons(VisualElement root, bool isLapel)
        {
            buttons = new List<(Button, MenuAction)>();
            foreach (var menu in menuActions)
            {
                if (menu.action.group == ToolbarGroup.Geometry)
                {
                    var button = new Button();
                    button.clicked += () => menu.PopulateAction();
                    button.text = isLapel ? StringReplace(menu.action.menuTitle) : menu.action.menuTitle;
                    button.tooltip = menu.action.tooltip.title;
                    button.style.fontSize = 10;
                    button.style.marginBottom = 1;
                    button.style.marginTop = 1;
                    button.style.marginLeft = 1;
                    button.style.marginRight = 1;
                    button.style.paddingLeft = 2;
                    button.style.paddingRight = 2;
                    button.style.borderLeftWidth = 0;
                    button.style.borderRightWidth = 0;
                    button.style.borderTopWidth = 0;
                    button.style.borderBottomWidth = 0;
                    root.Add(button);
                    buttons.Add((button, menu.action));
                }
            }
            Refresh();
        }

        void AddContextMenu(VisualElement root , string title , params ToolbarGroup[] groups )
        {
            var toolbar = new OverlayToolbar() { tooltip = title };
            var dropDown = new EditorToolbarDropdown() { text = title };
            dropDown.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var group in groups.Reverse())
                {
                    menu.AddDisabledItem(new GUIContent($"---------{group}---------") );

                    foreach (var menuAction in menuActions)
                    {
                        if (group == menuAction.action.group)
                        {
                            var enable = menuAction.action.enabled;

                            if (enable)
                            {
                                menu.AddItem(new GUIContent(menuAction.action.menuTitle), false, () => menuAction.action.PerformAction());
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent(menuAction.action.menuTitle,menuAction.action.tooltip.summary));
                            }
                        }
                    }
                }
                menu.ShowAsContext();
            };
            dropDown.style.fontSize = 12;

            toolbar.Add(dropDown);
            toolbar.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
            toolbar.style.marginBottom = 1;
            toolbar.style.marginTop = 1;
            toolbar.style.marginLeft = 1;
            toolbar.style.marginRight = 1;
            root.Add(toolbar);
        }
    }
}