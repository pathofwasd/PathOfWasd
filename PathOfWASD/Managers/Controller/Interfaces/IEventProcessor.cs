using System.Threading.Channels;

namespace PathOfWASD.Managers.Controller.Interfaces;

public interface IEventProcessor
{
    Task ProcessLoopAsync(ChannelReader<KeyEvent> reader, IKeyStateTracker tracker);
}