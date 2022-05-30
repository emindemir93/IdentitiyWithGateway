namespace BuildingBlocks.Infrastructure;

public interface ICallbackIntegrationMessage : IIntegrationMessage
{
    CallbackReturnType CallbackReturn { get; set; }
}
