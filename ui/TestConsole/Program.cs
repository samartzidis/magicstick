using HidLibrary;
using MagicStickUI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TestConsole
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Trace);
            });


            var devs = HidDevices.Enumerate(0x2E8A, 0xC010);
            foreach (var dev in devs)
            {
                Console.WriteLine(dev);
            }

            var d = HidDevices.GetDevice(@"\\?\hid#vid_2e8a&pid_c010&col05#6&d7c2e3&7&0004#{4d1e55b2-f16f-11cf-88cb-001111000030}");

            using var rpc = new Rpc(loggerFactory, d);
            rpc.Start();

            while (true)
            {
                var keymap = await rpc.GetKeymap();
                Console.WriteLine(JsonConvert.SerializeObject(keymap));
                var reply = await rpc.SetKeymap(new SetKeymapRequest { items = keymap.items });
                if (!reply.success)
                    break;
            }

            //await rpc.GetSettings();
        }
    }
}
