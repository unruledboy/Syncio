using Syncio.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Syncio.Common.Utils
{
    public class ProviderManager
    {
        private static readonly ProviderManager instance;

        static ProviderManager() => instance = new ProviderManager();

        private ProviderManager() { }

        public static ProviderManager Instance => instance;

        public T GetRequestProcessor<T>(SyncConfig config, string typeName) => GetProvivder<T>(config.Providers.Request.Processor, typeName);
        public T GetRequestTransport<T>(SyncConfig config, TaskConfig taskConfig, string typeName) => GetProvivder<T>(config.Providers.Request.Transport, typeName, taskConfig);
        public T GetSyncProcessor<T>(SyncConfig config, string typeName) => GetProvivder<T>(config.Providers.Sync.Processor, typeName);
        public T GetSyncTransport<T>(SyncConfig config, TaskConfig taskConfig, string typeName) => GetProvivder<T>(config.Providers.Sync.Transport, typeName, taskConfig);

        public T GetSyncPayloadLogProvider<T>(SyncConfig config, LogStrategy logStrategy) => GetProvivder<T>(config.Providers.Sync.PayloadLog, logStrategy.PayloadLogProvider);

        private static T GetProvivder<T>(List<ProviderSetting> providerSettings, string typeName, object parameter = null)
        {
            var provider = providerSettings.First(x => x.Type.AllEquals(typeName)).Provider;
            var type = Type.GetType(provider);
            var instance = parameter != null ? Activator.CreateInstance(type, parameter) : Activator.CreateInstance(type);
            return (T)instance;
        }
    }
}
