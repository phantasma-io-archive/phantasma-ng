using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Validator;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.API;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Phantasma.Node
{
    internal class Settings
    {
        public RPCSettings RPC { get; }
        public NodeSettings Node { get; }
        public AppSettings App { get; }
        public List<ValidatorSettings> Validators { get; }
        public LogSettings Log { get; }
        public WebhookSettings WebhookSetting { get; }
        public OracleSettings Oracle { get; }
        public SimulatorSettings Simulator { get; }
        public PerformanceMetricsSettings PerformanceMetrics { get; }

        public string _configFile;

        public static Settings Instance { get; private set; }

        private Settings(string[] args, IConfigurationSection section)
        {
            new CliArgumets(args);

            var levelSwitchConsole = new LoggingLevelSwitch
            {
                MinimumLevel = LogEventLevel.Verbose
            };

            var logConfig = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "{Timestamp:u} {Timestamp:ffff} [{Level:u3}] <{ThreadId}> {Message:lj}{NewLine}{Exception}",
                    levelSwitch: levelSwitchConsole);

            Serilog.Log.Logger = logConfig.CreateLogger();

            var defaultConfigFile = "config.json";

            this._configFile = defaultConfigFile;

            if (!File.Exists(_configFile))
            {
                Serilog.Log.Error($"Expected configuration file to exist: {this._configFile}");

                if (this._configFile == defaultConfigFile)
                {
                    Serilog.Log.Warning($"Copy either config_mainnet.json or config_testnet.json and rename it to {this._configFile}");
                }

                Environment.Exit(-1);
            }

            try
            {
                this.Node = new NodeSettings(args, section.GetSection("Node"));
                this.Simulator = new SimulatorSettings(section.GetSection("Simulator"));
                this.Oracle = new OracleSettings(section.GetSection("Oracle"));
                this.App = new AppSettings(section.GetSection("App"));
                this.Log = new LogSettings(section.GetSection("Log"));
                this.RPC = new RPCSettings(section.GetSection("RPC"));
                this.Validators = SetupValidatorsList(section.GetSection("Validators"));
                this.WebhookSetting = new WebhookSettings(section.GetSection("Webhook"));
                this.PerformanceMetrics = section.GetSection("PerformanceMetrics").Get<PerformanceMetricsSettings>();
            }
            catch (Exception e)
            {
                Serilog.Log.Error(e, $"There were issues loading settings from {this._configFile}, aborting...");
                Environment.Exit(-1);
            }
        }

        private static List<ValidatorSettings> SetupValidatorsList(IConfigurationSection section)
        {
            var validators = new List<ValidatorSettings>();

            var validatorsArray = section.GetChildren();
            //var validatorConfig = .Get<ValidatorConfig[]>().ToList();

            foreach (var validator in validatorsArray)
            {
                var validatorSettings = new ValidatorConfig(validator);
                validators.Add(new ValidatorSettings(validatorSettings.Address, validatorSettings.Name, validatorSettings.Host, validatorSettings.Port));
            }

            return validators;
        }

        public static void Load(string[] args, IConfigurationSection section)
        {
            Instance = new Settings(args, section);
        }

        public string GetInteropWif(PhantasmaKeys nodeKeys, string platformName)
        {
            var nexus = NexusAPI.GetNexus();

            var genesisHash = nexus.GetGenesisHash(nexus.RootStorage);
            var interopKeys = InteropUtils.GenerateInteropKeys(nodeKeys, genesisHash, platformName);
            var defaultWif = interopKeys.ToWIF();

            string customWIF = null;

            switch (platformName)
            {
                case "neo":
                    customWIF = this.Oracle.NeoWif;
                    break;


                case "ethereum":
                    customWIF = this.Oracle.EthWif;
                    break;
            }

            var result = !string.IsNullOrEmpty(customWIF) ? customWIF: defaultWif;

            if (result != null && result.Length == 64)
            {
                var temp = new PhantasmaKeys(Base16.Decode(result));
                result = temp.ToWIF();
            }

            return result;
        }
    }
}
