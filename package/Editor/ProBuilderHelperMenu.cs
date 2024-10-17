using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEditor.Actions;
using UnityEngine.UIElements;
using System.Drawing.Printing;
using PlasticPipe.PlasticProtocol.Messages;
using System.IO;

namespace UnityEditor.ProBuilder
{

    public class ProBuilderHelperMenu : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Tools/ProBuilder/OpenHelperMenu")]
        static void Open()
        {
            var window = GetWindow<ProBuilderHelperMenu>();
            window.titleContent = new GUIContent("ProBuilderHelperMenu");

        }

        protected struct MenuActionContent
        {
            public UnityEditor.ProBuilder.MenuAction action;
            public GUIContent content;
        }

        protected List<MenuActionContent> menuActions;
        protected Vector2 scrollPosition = Vector2.zero;
        protected bool[] folds;
        protected bool showAllActionMenu;


        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            GUIContent content = new GUIContent( $"Toggle Hide Action Menu");
            menu.AddItem(content, showAllActionMenu, ()=> showAllActionMenu = !showAllActionMenu);
        }

//        Texture texture;

        protected void OnEnable()
        {
//            texture = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.unity.probuilder/Content/Icons/Cursors/cutCursor.png");

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
                        content = new GUIContent(action.menuTitle, tooltip),
                    };
                    this.menuActions.Add(content);
                }


                UnityEditor.ProBuilder.ProBuilderEditor.selectionUpdated += (x) => Refresh();
                UnityEditor.ProBuilder.ProBuilderEditor.selectModeChanged += (x) => Refresh();
                UnityEditor.ProBuilder.ProBuilderEditor.afterMeshModification += (x) => Refresh();
                UnityEditor.ProBuilder.ProBuilderEditor.beforeMeshModification += (x) => Refresh();

                folds = Enumerable
                    .Range( 0 , System.Enum.GetValues(typeof(ToolbarGroup)).Cast<int>().Max() + 1 )
                    .Select( _ => true)
                    .ToArray();
            }
        }

        private void Refresh()
        {
            this.Repaint();
        }

        static readonly HashSet<Type> k_ContextMenuBlacklist = new HashSet<Type>()
        {
/*
            System.Type.GetType("UnityEditor.ProBuilder.Actions.OpenMaterialEditor,Unity.ProBuilder.Editor") ,
            System.Type.GetType("UnityEditor.ProBuilder.Actions.OpenUVEditor,Unity.ProBuilder.Editor") ,
            System.Type.GetType("UnityEditor.ProBuilder.Actions.OpenVertexColorEditor,Unity.ProBuilder.Editor") ,
*/
            System.Type.GetType("UnityEditor.ProBuilder.Actions.ToggleSelectBackFaces,Unity.ProBuilder.Editor") ,
            System.Type.GetType("UnityEditor.ProBuilder.Actions.ToggleHandleOrientation,Unity.ProBuilder.Editor") ,
            System.Type.GetType("UnityEditor.ProBuilder.Actions.ToggleDragRectMode,Unity.ProBuilder.Editor") ,
            System.Type.GetType("UnityEditor.ProBuilder.Actions.ToggleXRay,Unity.ProBuilder.Editor") ,
        };

        static DropdownMenuAction.Status GetStatus(MenuAction action)
        {
            if (action.hidden)
                return DropdownMenuAction.Status.Hidden;
            if (action.enabled)
                return DropdownMenuAction.Status.Normal;
            return DropdownMenuAction.Status.Disabled;
        }


        public void PopulateAction(MenuAction action)
        {
            var type = action.GetType();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            bool hasFileMenuEntry = (bool)type.GetProperty("hasFileMenuEntry", flags).GetValue(action);
            bool optionsEnabled = (bool)type.GetProperty("optionsEnabled", flags).GetValue(action);

            if( optionsEnabled)
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

        int buttonHorizontalCount = 2;

        private void OnGUI()
        {
            float containsHeight = 16;
            var initPos = this.position;
            float vPos = 4;

            GUI.Box(new Rect(4, 4, initPos.width - 8, containsHeight * 2 + 4 ),"");

            GUI.Label(new Rect(4, vPos, 128, containsHeight), $"Total Vertex");
            GUI.Label(new Rect(128, vPos, 128, containsHeight), $"{MeshSelection.totalVertexCount}");
            vPos += containsHeight;

            GUI.Label(new Rect(4, vPos, 128, containsHeight), $"Total Face");
            GUI.Label(new Rect(128, vPos, 128, containsHeight), $"{MeshSelection.totalFaceCount}");
            vPos += containsHeight;
            vPos += 4;

            containsHeight = 24;
            buttonHorizontalCount = (int)(initPos.width / 130);
            foreach (var group in Enum.GetValues(typeof(ToolbarGroup)).Cast<ToolbarGroup>())
            {
                GUI.enabled = true;
                folds[(int)group] = EditorGUI.Foldout(new Rect(0, vPos, initPos.width, containsHeight), folds[(int)group], group.ToString());
                vPos += containsHeight;
                if (folds[(int)group])
                {
                    int count = 0;
                    foreach (var action in menuActions)
                    {
                        var disp = action.action.group == group;

                        disp = disp & !k_ContextMenuBlacklist.Contains(action.GetType());

                        if (disp)
                        {
                            var x = count % buttonHorizontalCount;
                            var y = count / buttonHorizontalCount;
                            var w = initPos.width / buttonHorizontalCount;
                            var rect = new Rect(w * x, vPos + y * containsHeight, w, containsHeight);
                            GUI.enabled = action.action.enabled;

                            if ( GUI.Button(rect, action.content) )
                            {
                                PopulateAction(action.action);
                            }
                            count++;
                        }
                    }
                    vPos += ((count + buttonHorizontalCount -1 ) / buttonHorizontalCount) * containsHeight;
                }
            }
        }

    }
}