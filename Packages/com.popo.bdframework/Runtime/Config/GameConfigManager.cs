﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDFramework.Mgr;
using LitJson;

namespace BDFramework.Configure
{
    /// <summary>
    /// 配置属性
    /// 按Tag排序
    /// </summary>
    public class GameConfigAttribute : ManagerAttribute
    {
        public string Title = "";

        public GameConfigAttribute(int intTag, string tile) : base(intTag)
        {
            Title = tile;
        }

        public GameConfigAttribute(string tag) : base(tag)
        {
        }
    }

    /// <summary>
    /// 游戏配置中心
    /// </summary>
    public class GameConfigManager : ManagerBase<GameConfigManager, GameConfigAttribute>
    {
        /// <summary>
        /// config缓存
        /// </summary>
        private List<ConfigDataBase> configList { get; set; } = new List<ConfigDataBase>();

        /// <summary>
        /// start
        /// </summary>
        public override void Start()
        {
            base.Start();
            //执行
            var textAsset = BDLauncher.Inst.ConfigText;
            BDebug.Log("加载配置:"+ textAsset.name,"yellow");
            var (dataList, processorList) = LoadConfig(BDLauncher.Inst.ConfigText.text);
            configList = new List<ConfigDataBase>(dataList);
            for (int i = 0; i < dataList.Count; i++)
            {
                processorList[i].OnConfigLoad(dataList[i]);
            }
        }


        /// <summary>
        /// 加载配置
        /// </summary>
        /// <param name="configText"></param>
        /// <returns></returns>
        public (List<ConfigDataBase>, List<AConfigProcessor>) LoadConfig(string configText)
        {
            List<ConfigDataBase> retDatalist = new List<ConfigDataBase>();
            List<AConfigProcessor> retProcessorList = new List<AConfigProcessor>();

            var classDataList = this.GetAllClassDatas();
            var jsonObj = JsonMapper.ToObject(configText);
            //按tag顺序执行
            foreach (var cd in classDataList)
            {
                var nestType = cd.Type.GetNestedType("Config");
                if (nestType != null)
                {
                    foreach (JsonData jo in jsonObj)
                    {
                        //查询内部类
                        var clsTypeName = jo[nameof(ConfigDataBase.ClassType)].GetString();
                        //实例化内部类
                        if (nestType.FullName == clsTypeName)
                        {
                            var configData = JsonMapper.ToObject(nestType, jo.ToJson()) as ConfigDataBase;
                            var configProcessor = CreateInstance<AConfigProcessor>(cd);
                            retDatalist.Add(configData);
                            retProcessorList.Add(configProcessor);
                            break;
                        }
                    }
                }
            }

            return (retDatalist, retProcessorList);
        }


        /// <summary>
        /// 获取config
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetConfig<T>() where T : ConfigDataBase
        {
            var con = configList.FirstOrDefault((c) => c is T);
            return (T) con;
        }


        /// <summary>
        /// 创建新的配置
        /// </summary>
        /// <returns></returns>
        public List<ConfigDataBase> CreateNewConfig()
        {
            List<ConfigDataBase> list = new List<ConfigDataBase>();
            var allconfigtype = GameConfigManager.Inst.GetAllClassDatas();
            foreach (var cd in allconfigtype)
            {
                var configType = cd.Type.GetNestedType("Config");
                if (configType != null)
                {
                    var configInstance = Activator.CreateInstance(configType) as ConfigDataBase;
                    list.Add(configInstance);
                }
            }

            return list;
        }


        /// <summary>
        /// 读取config
        /// </summary>
        /// <param name="configText"></param>
        public Dictionary<Type,ConfigDataBase> ReadConfig(string configText)
        {
            //type=> 主class的type
            var map = new Dictionary<Type, ConfigDataBase>();
            
            var (datalist, processorlist) = LoadConfig(configText);
            //赋值新的
            var allconfigtype = GameConfigManager.Inst.GetAllClassDatas();
            foreach (var cd in allconfigtype)
            {
                var nestedType = cd.Type.GetNestedType("Config");
                if (nestedType != null)
                {
                    //寻找本地配置
                    var configData = datalist.FirstOrDefault((c) => c.ClassType == nestedType.FullName);
                    //不存在则创建新的配置对象
                    if (configData == null)
                    {
                        configData = Activator.CreateInstance(nestedType) as ConfigDataBase;
                    }

                    map[cd.Type] = configData;
                }
            }
            
            return map;
        }
        
    }
}
