using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.EditorCoroutines.Editor {

#if UNITY_EDITOR
    public class EditorCoroutineDebugger : EditorWindow {

        [Serializable]
        class BreakPoint : IComparable<BreakPoint> {
            public bool enabled = true;
            public StackFrame stackFrame;
            public String filePath;
            public int line;
            String _fileName;
            public String fileName {
                get {
                    if ( _fileName == null && !String.IsNullOrEmpty( filePath ) ) {
                        _fileName = Path.GetFileName( filePath );
                    }
                    return _fileName ?? String.Empty;
                }
            }
            public override string ToString() {
                return String.Format( "{0}-{1}", filePath, line );
            }
            public int CompareTo( BreakPoint other ) {
                var c = filePath.CompareTo( other.filePath );
                if ( c == 0 ) {
                    c = line.CompareTo( other.line );
                }
                return c;
            }
        }

        static Vector2 _breakpointsViewPos = Vector2.zero;
        List<BreakPoint> _breakpoints = new List<BreakPoint>();

        public enum DebugMode {
            Disable,
            BreakPoint,
            Step,
        }

        enum BreakpointState {
            Inactivated,
            Activated,
            Continue,
        }

        public DebugMode mode {
            get {
                return _debugMode;
            }
        }

        [NonSerialized]
        bool _inited = false;

        public static Func<int, WaitUntil> BreakPointFactory = skipFrames => DebugBreak( skipFrames );

        static DebugMode _debugMode = DebugMode.Disable;
        static BreakpointState _curBreakpointState = BreakpointState.Inactivated;
        static String _ui_current_frame = String.Empty;

        UnityEngine.Object _ui_breakpoint_filename = null;
        int _ui_breakpoint_line = 0;

        [MenuItem( "Window/Editor Coroutines/Debugger" )]
        static void Open() {
            _debugMode = ( DebugMode )EditorPrefs.GetInt( "EditorCoroutineDebugger-DebugMode", ( int )DebugMode.Step );
            EditorWindow.GetWindow( typeof( EditorCoroutineDebugger ) );
        }

        [MenuItem( "Window/Editor Coroutines/Run Demo 1" )]
        static void RunDemo1() {
            var window = EditorWindow.GetWindow( typeof( EditorCoroutineDebugger ) ) as EditorCoroutineDebugger;
            if ( window != null ) {
                window.StartCoroutine( Example_1() );
                window.PostRepaint();
            }
        }

        [MenuItem( "Window/Editor Coroutines/Run Demo 2" )]
        static void RunDemo2() {
            var window = EditorWindow.GetWindow( typeof( EditorCoroutineDebugger ) ) as EditorCoroutineDebugger;
            if ( window != null ) {
                window.StartCoroutine( Example_2() );
                window.PostRepaint();
            }
        }

        static IEnumerator Example_1() {
            UnityEngine.Debug.Log( "Editor Coroutine Example 1: begin" );
            yield return DebugBreak();
            UnityEngine.Debug.Log( "Editor Coroutine Example 1: step 1" );
            yield return new EditorWaitForSeconds( 5.0f );
            yield return DebugBreak();
            UnityEngine.Debug.Log( "Editor Coroutine Example 1: step 2" );
            yield return DebugBreak();
            UnityEngine.Debug.Log( "Editor Coroutine Example 1: step 3" );
            yield return DebugBreak();
            UnityEngine.Debug.Log( "Editor Coroutine Example 1 end." );
        }

        static IEnumerator Example_2() {
            UnityEngine.Debug.Log( "Editor Coroutine Example 2: begin" );
            var go = GameObject.CreatePrimitive( PrimitiveType.Sphere );
            var lightGO = new GameObject();
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            try {
                yield return DebugBreak();

                UnityEngine.Debug.Log( "Editor Coroutine Example 2: step 1" );
                light.color = Color.red;
                yield return DebugBreak();

                UnityEngine.Debug.Log( "Editor Coroutine Example 2: step 2" );
                light.color = Color.green;
                yield return DebugBreak();

                UnityEngine.Debug.Log( "Editor Coroutine Example 2: step 3" );
                light.color = Color.blue;
                yield return DebugBreak();

            } finally {
                if ( go != null ) { UnityEngine.Object.DestroyImmediate( go ); }
                if ( lightGO != null ) { UnityEngine.Object.DestroyImmediate( lightGO ); }
                UnityEngine.Debug.Log( "Editor Coroutine Example 2 end." );
            }
        }

        void OnDestroy() {
            _debugMode = DebugMode.Disable;
            EditorPrefs.SetInt( "EditorCoroutineDebugger-DebugMode", ( int )_debugMode );
        }

        static String GetProjectUnityRootPath() {
            var rootPath = Environment.CurrentDirectory.Replace( '\\', '/' );
            if ( Directory.Exists( rootPath ) ) {
                rootPath = Path.GetFullPath( rootPath );
                return rootPath.Replace( '\\', '/' );
            } else {
                return rootPath;
            }
        }

        static EditorCoroutineDebugger TryGet() {
            var array = Resources.FindObjectsOfTypeAll( typeof( EditorCoroutineDebugger ) );
            var window = ( ( array.Length != 0 ) ? ( array[ 0 ] as EditorCoroutineDebugger ) : null );
            return ( bool )window ? window : null;
        }

        void PostRepaint() {
            var _this = this;
            EditorApplication.delayCall += () => {
                if ( _this != null ) {
                    _this.Repaint();
                }
            };
        }

        void OnGUI() {
            if ( !_inited ) {
                _inited = true;
                _debugMode = ( DebugMode )EditorPrefs.GetInt( "EditorCoroutineDebugger-DebugMode", ( int )DebugMode.Step );
            }
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            var __debugMode = ( DebugMode )EditorGUILayout.EnumPopup( "Debug Mode", _debugMode );
            if ( EditorGUI.EndChangeCheck() ) {
                EditorPrefs.SetInt( "EditorCoroutineDebugger-DebugMode", ( int )__debugMode );
            }
            if ( _debugMode == DebugMode.Disable ) {
                if ( GUILayout.Button( "Break" ) ) {
                    __debugMode = DebugMode.Step;
                }
            }
            GUI.enabled = _curBreakpointState == BreakpointState.Activated;
            if ( GUILayout.Button( "Continue" ) ) {
                _curBreakpointState = BreakpointState.Continue;
                PostRepaint();
            }
            if ( _debugMode != DebugMode.Disable && !String.IsNullOrEmpty( _ui_current_frame ) ) {
                EditorGUILayout.HelpBox( _ui_current_frame, _curBreakpointState == BreakpointState.Activated ? MessageType.Error : MessageType.Warning );
            }
            _debugMode = __debugMode;

            GUI.enabled = true;
            _breakpointsViewPos = EditorGUILayout.BeginScrollView( _breakpointsViewPos );
            {
                List<int> removeList = null;
                for ( int i = 0; i < _breakpoints.Count; ++i ) {
                    EditorGUILayout.BeginHorizontal();
                    var bp = _breakpoints[ i ];
                    EditorGUI.BeginChangeCheck();
                    bp.enabled = EditorGUILayout.ToggleLeft( "", bp.enabled, GUILayout.Width( 16 ) );
                    if ( EditorGUI.EndChangeCheck() && Event.current.shift ) {
                        for ( int j = i + 1; j < _breakpoints.Count; ++j ) {
                            _breakpoints[ j ].enabled = bp.enabled;
                        }
                    }
                    GUI.enabled = false;
                    EditorGUILayout.TextField( bp.fileName, GUILayout.MinWidth( 200 ) );
                    GUI.enabled = true;
                    bp.line = EditorGUILayout.IntField( bp.line, GUILayout.Width( 60 ) );
                    if ( GUILayout.Button( "Goto", GUILayout.Width( 60 ) ) ) {
                        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(
                            bp.filePath.Replace( '/', Path.DirectorySeparatorChar ), bp.line, 0 );
                    }
                    if ( GUILayout.Button( "Delete", GUILayout.Width( 60 ) ) ) {
                        removeList = removeList ?? new List<int>();
                        removeList.Add( i );
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if ( removeList != null && removeList.Count > 0 ) {
                    removeList.Reverse();
                    removeList.ForEach( k => _breakpoints.RemoveAt( k ) );
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            _ui_breakpoint_filename = EditorGUILayout.ObjectField( _ui_breakpoint_filename, typeof( UnityEngine.Object ), false, GUILayout.MinWidth( 200 ) );
            if ( EditorGUI.EndChangeCheck() && _ui_breakpoint_filename != null ) {
                var scriptAssetPath = AssetDatabase.GetAssetPath( _ui_breakpoint_filename );
                if ( String.IsNullOrEmpty( scriptAssetPath ) || !scriptAssetPath.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) ) {
                    _ui_breakpoint_filename = null;
                }
            }

            _ui_breakpoint_line = EditorGUILayout.IntField( _ui_breakpoint_line, GUILayout.Width( 60 ) );
            if ( GUILayout.Button( "Add", GUILayout.Width( 40 ) ) ) {
                var scriptAssetPath = AssetDatabase.GetAssetPath( _ui_breakpoint_filename );
                if ( !String.IsNullOrEmpty( scriptAssetPath ) ) {
                    var bp = new BreakPoint();
                    bp.enabled = true;
                    bp.filePath = GetProjectUnityRootPath() + "/" + scriptAssetPath;
                    bp.line = _ui_breakpoint_line;
                    var key = bp.ToString();
                    var index = _breakpoints.FindIndex( e => e.ToString() == key );
                    if ( index >= 0 ) {
                        _breakpoints.RemoveAt( index );
                    }
                    _breakpoints.Add( bp );
                    _breakpoints.Sort();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        public static WaitUntil DebugBreak( int skipFrames = 1 ) {
            var frameKey = String.Empty;
            var stackInfo = String.Empty;
            var curFrameInfo = String.Empty;
            if ( _debugMode != DebugMode.Disable ) {
                var stackTrace = new StackTrace( skipFrames, true );
                var stackFrames = stackTrace.GetFrames();
                if ( stackFrames.Length > 0 ) {
                    var frame = stackFrames[ 0 ];
                    var filePath = frame.GetFileName().Replace( '\\', '/' );
                    var line = frame.GetFileLineNumber();
                    frameKey = String.Format( "{0}-{1}", filePath, line );
                    curFrameInfo = String.Format( "{0}(at {1}: {2})", frame.GetMethod(), frame.GetFileName(), frame.GetFileLineNumber() );
                    String ret = String.Empty;
                    var sb = new StringBuilder( 200 );
                    for ( int i = 0; i < stackFrames.Length; ++i ) {
                        var _frame = stackFrames[ i ];
                        sb.Append( _frame.GetMethod() );
                        sb.Append( "(at " );
                        sb.Append( _frame.GetFileName() );
                        sb.Append( ": " );
                        sb.Append( _frame.GetFileLineNumber() );
                        sb.Append( ")" );
                        sb.AppendLine();
                    }
                    stackInfo = sb.ToString();
                }
            }
            var focus = false;
            return new WaitUntil(
                () => {
                    if ( _debugMode != DebugMode.Disable && !focus ) {
                        FocusWindowIfItsOpen<EditorCoroutineDebugger>();
                        focus = true;
                    }
                    switch ( _curBreakpointState ) {
                    case BreakpointState.Inactivated: {
                            switch ( _debugMode ) {
                            case DebugMode.Step: {
                                    _curBreakpointState = BreakpointState.Activated;
                                    _ui_current_frame = curFrameInfo;
                                    var _stackInfo = stackInfo;
                                    if ( !String.IsNullOrEmpty( _stackInfo ) ) {
                                        UnityEngine.Debug.Log( _stackInfo );
                                    }
                                }
                                break;
                            case DebugMode.BreakPoint: {
                                    _curBreakpointState = BreakpointState.Continue;
                                    _ui_current_frame = curFrameInfo;
                                    var _stackInfo = stackInfo;
                                    if ( !String.IsNullOrEmpty( _stackInfo ) ) {
                                        UnityEngine.Debug.Log( _stackInfo );
                                    }
                                    BreakPoint bp;
                                    var _frameKey = frameKey;
                                    var _breakpoints = TryGet()?._breakpoints;
                                    if ( _breakpoints != null && !String.IsNullOrEmpty( frameKey ) && ( bp = _breakpoints.Find( e => e.ToString() == _frameKey ) ) != null ) {
                                        if ( bp.enabled ) {
                                            _curBreakpointState = BreakpointState.Activated;
                                        }
                                    }
                                }
                                break;
                            case DebugMode.Disable:
                                _ui_current_frame = String.Empty;
                                _curBreakpointState = BreakpointState.Continue;
                                break;
                            }
                        }
                        break;
                    case BreakpointState.Activated:
                        break;
                    case BreakpointState.Continue:
                        _curBreakpointState = BreakpointState.Inactivated;
                        return true;
                    }
                    return false;
                }
            );
        }
    }
#endif
}

//EOF
