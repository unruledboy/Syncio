using System.Collections.Generic;

namespace Syncio.Common.Models
{
    public class Provider
    {
        public List<ProviderSetting> Processor { get; set; }
        public List<ProviderSetting> Transport { get; set; }
        public List<ProviderSetting> PayloadLog { get; set; }
    }
}
