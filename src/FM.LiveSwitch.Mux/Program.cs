using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    partial class Program
    {
        static void Main(string[] args)
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            using (var parser = new Parser((settings) =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            }))
            {
                var result = parser.ParseArguments<MuxOptions>(args);

                result.WithParsed(options =>
                {
                    Task.Run(async () =>
                    {
                        await new Muxer(options).Run();
                    }).GetAwaiter().GetResult();
                });

                result.WithNotParsed(errors =>
                {
                    var helpText = HelpText.AutoBuild(result);
                    helpText.Copyright = "Copyright (C) 2019 Frozen Mountain Software Ltd.";
                    helpText.AddEnumValuesToHelpText = true;
                    helpText.AddOptions(result);
                    Console.Error.Write(helpText);
                    Environment.Exit(1);
                });
            }
        }
    }
}
