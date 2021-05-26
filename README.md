---
page_type: sample
languages:
- csharp
products:
- azure
- azure-media-services
---

# Azure Media Services Explorer (for AMS v3)

Azure Media Services Explorer (AMSE) is a .NET 5.0 (C#) application for Windows that does upload, download, encode and stream VOD and live content with [Azure Media Services v3](https://azure.microsoft.com/en-us/services/media-services/).

See a full description [here](http://azure.microsoft.com/blog/2014/10/08/managing-media-workflows-with-the-new-azure-media-services-explorer-tool).

**The latest binary for Windows (with a MSI installer) is available in the [releases section](https://github.com/Azure/Azure-Media-Services-Explorer/releases)**. Please note that AMSE v5 is for AMS v3 API (main branch), and AMSE v4 is for AMS v2 (AMSv2 branch).
This application requires [.NET 5.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/5.0) to run.

AMSE uses Application Insights for Telemetry. This feature can be turned off in Options.

## Prerequisites to compile

This solution has been developed using [Visual Studio 2019 16.10.0](https://visualstudio.microsoft.com/vs/). It contains two projects: "AMSExplorer", the main application, and "Setup", a project that creates a Setup executable (EXE/MSI).

You need to install the [Microsoft Visual Studio Installer Projects](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2017InstallerProjects) in order to open and build the Setup project in Visual Studio.

This solution requires [.NET 5.0 SDK to compile](https://dotnet.microsoft.com/download/dotnet/5.0).

## Contacts

Contact: amse@microsoft.com

Open a bug [here](https://github.com/Azure/Azure-Media-Services-Explorer/issues/new).

![Screen capture](https://user-images.githubusercontent.com/8104205/116678834-17935c80-a9aa-11eb-9419-6c79de82b8ca.png)

## Contributing

This project welcomes contributions and suggestions. Please see our [contributing guide](CONTRIBUTING.md).

This project has adopted the [Microsoft Open Source Code of Conduct](CODE_OF_CONDUCT.md).

## Data Collection

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at <https://go.microsoft.com/fwlink/?LinkID=824704>. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
