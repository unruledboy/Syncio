using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Syncio.Common;
using Syncio.Common.Interfaces;
using Syncio.Common.Models;
using Syncio.Common.Utils;

namespace Syncio.Transport.AzureServiceBus
{
	public class AzureServiceBusTransportSync : ISyncTransport
	{
		public event EventHandler<ProgressEventArgs> Progress;
		private readonly TaskConfig config;
		private SubscriptionClient subscriptionClient;
		private Func<SyncRequest, object, SyncResult> onRequest;

		public AzureServiceBusTransportSync(TaskConfig config)
		{
			this.config = config;
		}

		public async void Stop()
		{
			if (subscriptionClient != null)
				await subscriptionClient.CloseAsync();
		}

		public async void Start(Func<SyncRequest, object, SyncResult> onRequest)
		{
			Stop();

			var topic = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyTopic)).Value;
			var topicFilter = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyTopicFilter)).Value;
			var transportId = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyId)).Value;
			var connectionString = config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyConnectionString)).Value;
			var managementClient = new ManagementClient(connectionString);
			var subscription = string.Format("{0}{1}", topicFilter, transportId).Replace("-", string.Empty);
			if (!(await managementClient.TopicExistsAsync(topic)))
			{
				var td = new TopicDescription(topic)
				{
					MaxSizeInMB = Convert.ToInt64(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyMaxSizeInMegabytes)).Value),
					DefaultMessageTimeToLive = TimeSpan.FromSeconds(Convert.ToInt64(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyDefaultMessageTimeToLiveInSecond)).Value))
				};
				await managementClient.CreateTopicAsync(td);
				Progress?.Invoke(this, new ProgressEventArgs { Level = LogLevel.Important, Message = $"Topic created: {topic}" });
			}

			if (!(await managementClient.SubscriptionExistsAsync(topic, subscription)))
			{
				Progress?.Invoke(this, new ProgressEventArgs { Level = LogLevel.Important, Message = $"Subscription created: {subscription}" });
				await managementClient.CreateSubscriptionAsync(topic, subscription);
			}

			this.onRequest = onRequest;

			var options = new MessageHandlerOptions(ExceptionReceivedHandler)
			{
				AutoComplete = false,
				MaxAutoRenewDuration = TimeSpan.FromSeconds(Convert.ToInt32(config.Transport.Settings.First(x => x.Name.AllEquals(TransportConstants.KeyAutoRenewTimeoutInSecond)).Value))
			};
			subscriptionClient = new SubscriptionClient(connectionString, topic, subscription, ReceiveMode.PeekLock);
			subscriptionClient.RegisterMessageHandler(ProcessMessageAsync, options);
		}

		async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
		{
			Progress?.Invoke(this, new ProgressEventArgs { Message = $"Received message id {message.MessageId}" });
			if (message.UserProperties.TryGetValue(Constants.KeyName, out var name) && name is string nameValue)
			{
				if (message.UserProperties.TryGetValue(Constants.KeyId, out var id) && id is long idValue
					&& message.UserProperties.TryGetValue(Constants.KeyTaskType, out var taskType) && taskType is string taskTypeValue
					&& message.UserProperties.TryGetValue(Constants.KeyType, out var type) && type is int typeValue
					&& message.UserProperties.TryGetValue(Constants.KeyOperation, out var operation) && operation is int operationValue
					&& message.UserProperties.TryGetValue(Constants.KeyResourceId, out var resourceIds) && resourceIds is string resourceIdsValue
					&& message.UserProperties.TryGetValue(Constants.KeyCreatedDate, out var createdDate) && createdDate is DateTime createdDateValue
					&& message.UserProperties.TryGetValue(Constants.KeyPayload, out var payload))
				{
					var request = new SyncRequest { Id = idValue, Type = typeValue, Operation = (Operation)operationValue, ResourceIds = Serializer.DeserializeText<List<object>>(resourceIdsValue), CreatedDate = createdDateValue };
                    var result = onRequest(request, payload);
                    if (!result.IsSuccessful)
                        Progress?.Invoke(this, new ProgressEventArgs { Message = $"Failed to process request for task {name}", Request = request });

                    await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
				}
				else
					Progress?.Invoke(this, new ProgressEventArgs { Message = $"Missing request info find task {name}" });
			}
			else
				Progress?.Invoke(this, new ProgressEventArgs { Message = $"Could not find task {name}" });
		}

		Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
		{
			Progress?.Invoke(this, new ProgressEventArgs { Message = $"Message handler encountered an exception: {exceptionReceivedEventArgs.Exception}" });
			return Task.CompletedTask;
		}

        public void LogPayload(SyncConfig config, TaskConfig task, SyncRequest request, object payload) => ProviderManager.Instance.GetSyncPayloadLogProvider<ILogger>(config, task.LogStrategy)?.Log(payload as string, request: request);
    }
}
