using Pulumi;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.App.Inputs;
using Pulumi.Docker;
using Pulumi.Docker.Inputs;
using Config = Pulumi.Config;
using ContainerArgs = Pulumi.AzureNative.App.Inputs.ContainerArgs;
using SecretArgs = Pulumi.AzureNative.App.Inputs.SecretArgs;

return await Pulumi.Deployment.RunAsync(() =>
{
    var config = new Config();
    var providerConfig = new Config("azure-native");

    var resourceGroup = new ResourceGroup(
        "resource-group",
        new ResourceGroupArgs { ResourceGroupName = "cok-memes" },
        new CustomResourceOptions
        {
            ImportId =
                $"/subscriptions/{providerConfig.Require("subscriptionId")}/resourceGroups/cok-memes",
            Protect = true
        }
    );

    var logAnalytics = new Workspace(
        "log-analytics",
        new WorkspaceArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new WorkspaceSkuArgs { Name = WorkspaceSkuNameEnum.PerGB2018 },
            RetentionInDays = 30
        }
    );

    var logAnalyticsSharedKeys = Output
        .Tuple(resourceGroup.Name, logAnalytics.Name)
        .Apply(
            items =>
                GetSharedKeys.InvokeAsync(
                    new GetSharedKeysArgs
                    {
                        ResourceGroupName = items.Item1,
                        WorkspaceName = items.Item2,
                    }
                )
        );

    var environment = new ManagedEnvironment(
        "environment",
        new ManagedEnvironmentArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AppLogsConfiguration = new AppLogsConfigurationArgs
            {
                Destination = "log-analytics",
                LogAnalyticsConfiguration = new LogAnalyticsConfigurationArgs
                {
                    CustomerId = logAnalytics.CustomerId,
                    SharedKey = logAnalyticsSharedKeys.Apply(r => r.PrimarySharedKey!)
                }
            }
        }
    );

    const string imageRegistryServer = "docker.io";
    var imageRegistryUsername = config.Require("imageRegistryUsername");

    var botImage = new Image(
        "bot-image",
        new ImageArgs
        {
            ImageName = "uncledave/cok-bot:latest",
            Build = new DockerBuildArgs
            {
                Context = "..",
                Dockerfile = "../ChampionsOfKhazad.Bot/Dockerfile",
                Platform = "linux/amd64"
            },
            Registry = new RegistryArgs
            {
                Server = imageRegistryServer,
                Username = imageRegistryUsername,
                Password = config.RequireSecret("imageRegistryWritePassword")
            }
        }
    );

    const string botTokenSecretName = "bot-token";
    const string imageRegistryReadPasswordSecretName = "registry-read-password";
    const string openAiApiKeySecretName = "open-ai-api-key";
    const string pineconeApiKeySecretName = "pinecone-api-key";

    var containerApp = new ContainerApp(
        "bot-app",
        new ContainerAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ManagedEnvironmentId = environment.Id,
            Configuration = new ConfigurationArgs
            {
                Registries = new RegistryCredentialsArgs
                {
                    Server = imageRegistryServer,
                    Username = imageRegistryUsername,
                    PasswordSecretRef = imageRegistryReadPasswordSecretName
                },
                Secrets =
                {
                    new SecretArgs
                    {
                        Name = botTokenSecretName,
                        Value = config.RequireSecret("botToken")
                    },
                    new SecretArgs
                    {
                        Name = imageRegistryReadPasswordSecretName,
                        Value = config.RequireSecret("imageRegistryReadPassword")
                    },
                    new SecretArgs
                    {
                        Name = openAiApiKeySecretName,
                        Value = config.RequireSecret("openAiApiKey")
                    },
                    new SecretArgs
                    {
                        Name = pineconeApiKeySecretName,
                        Value = config.RequireSecret("pineconeApiKey")
                    }
                }
            },
            Template = new TemplateArgs
            {
                Containers = new ContainerArgs
                {
                    Name = "bot",
                    Image = botImage.RepoDigest!,
                    Env =
                    {
                        new EnvironmentVarArgs
                        {
                            Name = "Bot__Token",
                            SecretRef = botTokenSecretName
                        },
                        new EnvironmentVarArgs
                        {
                            Name = "DOTNET_ENVIRONMENT",
                            Value = config.Require("environment")
                        },
                        new EnvironmentVarArgs
                        {
                            Name = "OpenAIServiceOptions__ApiKey",
                            SecretRef = openAiApiKeySecretName
                        },
                        new EnvironmentVarArgs
                        {
                            Name = "Pinecone__ApiKey",
                            SecretRef = pineconeApiKeySecretName
                        }
                    },
                    Resources = new ContainerResourcesArgs { Cpu = .25, Memory = "0.5Gi" }
                },
                Scale = new ScaleArgs { MinReplicas = 1, MaxReplicas = 1 }
            }
        }
    );
});
