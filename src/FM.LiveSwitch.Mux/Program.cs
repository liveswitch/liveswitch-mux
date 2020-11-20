using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(options =>
                {
                    // reserve stdout for programmatic use
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
            });

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            using var parser = new Parser((settings) =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var result = parser.ParseArguments<MuxOptions>(args);

            result.WithParsed(options =>
            {
                Task.Run(async () =>
                {
                    await new Muxer(options, loggerFactory).Run();
                }).GetAwaiter().GetResult();
            });

            result.WithNotParsed(errors =>
            {
                var helpText = HelpText.AutoBuild(result, 96);
                helpText.Copyright = "Copyright (C) 2019 Frozen Mountain Software Ltd.";
                helpText.AddEnumValuesToHelpText = true;
                helpText.AddOptions(result);
                Console.Error.Write(helpText);
                Environment.Exit(1);
            });
        }
    }
}
