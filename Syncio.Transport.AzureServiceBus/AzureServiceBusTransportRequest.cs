using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Transport.AzureServiceBus
{
    public class AzureServiceBusTransportRequest : IRequestTransport
	{
		public event EventHandler<ProgressEventArgs> Progress;
		private readonly TaskConfig config;
		private TopicClient topicClient;

		public AzureServiceBusTransportRequest(TaskConfig config)
		{
			this.config = config;
		}

		public async Task<bool> Send(SyncRequest request, object payload)
		{
			if (topicClient == null)
			{
				var connectionString = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyConnectionString)).Value;
				var managementClient = new ManagementClient(connectionString);
				var topic = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyTopic)).Value;
				if (!(await managementClient.TopicExistsAsync(topic)))
				{
					var td = new TopicDescription(topic)
					{
						EnableBatchedOperations = Convert.ToBoolean(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyEnableExpress)).Value),
						MaxSizeInMB = Convert.ToInt64(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyMaxSizeInMegabytes)).Value),
						DefaultMessageTimeToLive = TimeSpan.FromSeconds(Convert.ToInt64(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyDefaultMessageTimeToLiveInSecond)).Value))
					};
					await managementClient.CreateTopicAsync(td);
					Progress?.Invoke(this, new ProgressEventArgs { Level = LogLevel.Important, Message = $"Topic created: {topic}" });
				}

				var topicFilter = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyTopicFilter)).Value;
				if (!(await managementClient.SubscriptionExistsAsync(topic, topicFilter)))
				{
					await managementClient.CreateSubscriptionAsync(topic, topicFilter);
					Progress?.Invoke(this, new ProgressEventArgs { Level = LogLevel.Important, Message = $"Subscription created: {topicFilter}" });
				}

				topicClient = new TopicClient(connectionString, topic);
			}
			var buffer = payload != null ? Serializer.Serialize(payload) : null;
			var message = buffer != null ? new Message(buffer) : new Message();
			//message.DeliveryCount = 1;

			message.UserProperties.Add(Constants.KeyName, request.TaskName);
			message.UserProperties.Add(Constants.KeyTaskType, request.TaskType);
			message.UserProperties.Add(Constants.KeyId, request.Id);
			message.UserProperties.Add(Constants.KeyOperation, (int)request.Operation);
			message.UserProperties.Add(Constants.KeyType, request.Type);
			message.UserProperties.Add(Constants.KeyResourceId, Serializer.SerializeText(request.ResourceIds));
			message.UserProperties.Add(Constants.KeyCreatedDate, request.CreatedDate);
			message.UserProperties.Add(Constants.KeyPayload, Serializer.SerializeText(payload)); //todo:what if oversize? Will use BLOB, I guess

			await topicClient.SendAsync(message);

			//todo: handle exception here?

			return true;
		}
	}
}
