using System;
using System.Reflection;
using BDFramework.Asset;
using BDFramework.Core.Tools;
using BDFramework.GameStart;
using UnityEngine;
using BDFramework.ResourceMgr;
using BDFramework.Sql;
using LitJson;
using Sirenix.OdinInspector;


namespace BDFramework
{
    [RequireComponent(typeof(Config))]
    public class BDLauncher : MonoBehaviour
    {
        /// <summary>
        /// 框架版本号
        /// </summary>
        public const string Version  = "2.1.0";

        /// <summary>
        /// 客户端配置信息
        /// </summary>
        [HideInInspector]
        public GameConfig GameConfig { get; private set; }
        
        /// <summary>
        /// 客户端包信息
        /// </summary>
         public ClientPackageBuildInfo ClientBuildInfo { get;  set; }
        
        /// <summary>
        /// Config的Text
        /// </summary>
        public TextAsset ConfigText;

        #region 对外的生命周期

        public delegate void GameLauncherDelegate();

        static public GameLauncherDelegate OnUpdate { get; set; }
        static public GameLauncherDelegate OnLateUpdate { get; set; }

        #endregion

        static public BDLauncher Inst { get; private set; }

        // Use this for initialization
        private void Awake()
        {
            Inst = this;
            //添加组件
            this.gameObject.AddComponent<IEnumeratorTool>();
            var debug = this.gameObject.GetComponent<BDebug>();
            var platform = BApplication.RuntimePlatform;
            //游戏配置
            if (this.ConfigText)
            {
                BDebug.Log("配置:" + this.ConfigText.name);

                this.GameConfig = JsonMapper.ToObject<GameConfig>(this.ConfigText.text);
                //纠正配置
                if (!Application.isEditor)
                {
                    if (this.GameConfig.ArtRoot != AssetLoadPathType.Persistent &&
                        this.GameConfig.ArtRoot != AssetLoadPathType.StreamingAsset)
                    {
                        this.GameConfig.ArtRoot = AssetLoadPathType.Persistent;
                    }

                    if (this.GameConfig.SQLRoot != AssetLoadPathType.Persistent &&
                        this.GameConfig.SQLRoot != AssetLoadPathType.StreamingAsset)
                    {
                        this.GameConfig.SQLRoot = AssetLoadPathType.Persistent;
                    }

                    if (this.GameConfig.CodeRoot == AssetLoadPathType.DevOpsPublish)
                    {
                        this.GameConfig.CodeRoot = AssetLoadPathType.Persistent;
                    }
                }
            }
            else
            {
                BDebug.LogError("GameConfig配置为null,请检查!");
            }

            //日志打印
            debug.IsLog = this.GameConfig.IsDebugLog;
            //添加不删除的组件
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(this);
            }
        }


        /// <summary>

        #region 启动热更逻辑

        /// <summary>
        /// 初始化
        /// 修改版本,让这个启动逻辑由使用者自行处理
        /// </summary>
        /// <param name="mainProjectTypes">Editor模式下,UPM隔离了DLL需要手动传入</param>
        /// <param name="GameId">单游戏更新启动不需要id，多游戏更新需要id号</param>
        public void Launch(Type[] mainProjectTypes, Action<bool> clrBindingAction, string gameId = "default",Action loadEndCallback = null)
        {
            BDebug.Log("【Launch】Persistent:" + Application.persistentDataPath);
            BDebug.Log("【Launch】StreamingAsset:" + Application.streamingAssetsPath);
            //主工程启动
            IGameStart mainStart;
            foreach (var type in mainProjectTypes)
            {
                //TODO 这里有可能先访问到 IGamestart的Adaptor
                if (type.IsClass && type.GetInterface(nameof(IGameStart)) != null)
                {
                    BDebug.Log("【Launch】主工程Start! " + type.FullName);
                    mainStart = Activator.CreateInstance(type) as IGameStart;
                    if (mainStart != null)
                    {
                        //注册
                        mainStart.Start();
                        OnUpdate += mainStart.Update;
                        OnLateUpdate += mainStart.LateUpdate;
                        break;
                    }
                }
            }


            BDebug.Log("【Launch】框架资源版本验证!");
            //开始资源检测
            ClientAssetsHelper.CheckBasePackageVersion(BApplication.RuntimePlatform, () =>
            {
                //1.美术目录
                BResources.Init(GameConfig.ArtRoot);
                //2.sql
                SqliteLoder.Init(GameConfig.SQLRoot);
                //3.脚本,这个启动会开启所有的逻辑
                ScriptLoder.Init(GameConfig.CodeRoot, GameConfig.CodeRunMode, mainProjectTypes, clrBindingAction);
                //触发回调
                loadEndCallback?.Invoke();
            });
        }

        #endregion

        #region 生命周期

        //普通帧循环
        private void Update()
        {
            OnUpdate?.Invoke();
        }

        //更快的帧循环
        private void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }

        void OnApplicationQuit()
        {
#if UNITY_EDITOR
            SqliteLoder.Close();
            ILRuntimeHelper.Dispose();
#endif
        }

        #endregion
    }
}
