// using System;
// using System.Buffers;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using System.Threading.Tasks;

// using DotNetCore.CAP;
// using DotNetCore.CAP.Messages;
// using DotNetCore.CAP.Serialization;

// using Microsoft.Extensions.Options;
// using BuildingBlocks.ApplicationUser;
// using BuildingBlocks.CorrelationId;
// using BuildingBlocks.Infrastructure.JsonConverters;

// namespace BuildingBlocks.Infrastructure
// {
//     public class CustomSerializer : ISerializer
//     {
//         private const string CorrelationHeader = "X-Correlation-ID";
//         private const string ApplicationUserHeader = "X-Application-User";
//         private readonly JsonUtf8Serializer baseSerializer;
//         private readonly ICorrelationContextFactory correlationContextFactory;
//         private readonly ICorrelationContextAccessor correlationContextAccessor;
//         private readonly IApplicationUserAccessor applicationUserAccessor;
//         private readonly IOptions<CapOptions> capOptions;

//         public CustomSerializer(ICorrelationContextFactory correlationContextFactory,
//             ICorrelationContextAccessor correlationContextAccessor,
//             IApplicationUserAccessor applicationUserAccessor, IOptions<CapOptions> capOptions)
//         {
//             this.capOptions = capOptions;
//             this.capOptions.Value.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
//             this.capOptions.Value.JsonSerializerOptions.Converters.Add(BooleanConverter.GetConverter());
//             this.capOptions.Value.JsonSerializerOptions.Converters.Add(DateTimeConverter.GetConverter());
//             this.capOptions.Value.JsonSerializerOptions.Converters.Add(DecimalConverter.GetConverter());
//             this.capOptions.Value.JsonSerializerOptions.Converters.Add(EnumConverter<CallbackReturnType>.GetConverter());


//             this.baseSerializer = new JsonUtf8Serializer(this.capOptions);
//             this.correlationContextFactory = correlationContextFactory;
//             this.correlationContextAccessor = correlationContextAccessor;
//             this.applicationUserAccessor = applicationUserAccessor;

//         }

//         public Message Deserialize(string json)
//         {
//             return this.baseSerializer.Deserialize(json);
//         }

//         public object Deserialize(object value, Type valueType)
//         {
//             var removeString = "data:IntegrationMessage;base64,";
//             var isBase64 = value.ToString().Contains("data:IntegrationMessage;base64,");
//             var valueString = "";
//             if (isBase64) {
//                 valueString = value.ToString().Remove(0, removeString.Count());
//                 byte[] byteArray = Convert.FromBase64String(valueString);
//                 var obj = JsonSerializer.Deserialize(byteArray, typeof(JsonElement), this.capOptions.Value.JsonSerializerOptions);
//                 return this.baseSerializer.Deserialize(obj, valueType);

//             }

//             return this.baseSerializer.Deserialize(value, valueType);

//         }

//         public Task<Message> DeserializeAsync(TransportMessage transportMessage, Type valueType)
//         {

//             if (transportMessage.Headers.TryGetValue(CorrelationHeader, out var correlationStr))
//             {
//                 var guid = Guid.Parse(correlationStr);
//                 this.correlationContextFactory.Create(guid, CorrelationHeader);
//             }

//             if (transportMessage.Headers.TryGetValue(ApplicationUserHeader, out var userStr))
//             {
//                 var user = JsonSerializer.Deserialize<ApplicationUser.ApplicationUser>(userStr);
//                 this.applicationUserAccessor.CurrentUser = user;
//             }
//             if (valueType == typeof(Message))
//             {
//                 var obj = JsonSerializer.Deserialize(transportMessage.Body, typeof(JsonElement), this.capOptions.Value.JsonSerializerOptions);
//                 return Task.FromResult(new Message(transportMessage.Headers, obj));
//             }



//             return this.baseSerializer.DeserializeAsync(transportMessage, valueType);
//         }

//         public bool IsJsonType(object jsonObject)
//         {
//             return this.baseSerializer.IsJsonType(jsonObject);
//         }

//         public string Serialize(Message message)
//         {
//             return this.baseSerializer.Serialize(message);
//         }

//         public Task<TransportMessage> SerializeAsync(Message message)
//         {
//             if (message.Value is IntegrationMessage integration)
//             {
//                 if (integration.CorrelationId != Guid.Empty)
//                 {
//                     message.Headers.TryAdd(CorrelationHeader, integration.CorrelationId.ToString());
//                 }

//                 if (integration.User != null)
//                 {
//                     var user = JsonSerializer.Serialize(integration.User);
//                     message.Headers.TryAdd(ApplicationUserHeader, user);
//                 }
//                 var obj = JsonDocument.Parse(integration.Inner);
//                 var bytes = JsonSerializer.SerializeToUtf8Bytes(obj);
//                 var newMessage = new TransportMessage(message.Headers, bytes);
//                 return Task.FromResult(newMessage);

//             }
//             else if (message.Value is JsonElement jel)
//             {
//                 if (jel.TryGetProperty("Inner", out var innerProp))
//                 {

//                     var inner = innerProp.GetString();
//                     if (!string.IsNullOrWhiteSpace(inner))
//                     {

//                         var correlationId = jel.GetProperty("CorrelationId").GetString();
//                         if (!string.IsNullOrWhiteSpace(correlationId))
//                         {
//                             message.Headers.TryAdd(CorrelationHeader, correlationId);
//                         }

//                         var user = jel.GetProperty("User").ToString();
//                         if (!string.IsNullOrWhiteSpace(user))
//                         {
//                             message.Headers.TryAdd(ApplicationUserHeader, user);
//                         }

//                         var obj = JsonDocument.Parse(inner);
//                         var bytes = JsonSerializer.SerializeToUtf8Bytes(obj);
//                         var newMessage = new TransportMessage(message.Headers, bytes);
//                         return Task.FromResult(newMessage);
//                     }
//                 }

//             }

//             return this.baseSerializer.SerializeAsync(message);
//         }
//     }
// }
