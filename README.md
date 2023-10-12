<p align="center">
  <img
    src="/logo.png"
    width="125px"
  >
</p>

<h1 align="center">Phantasma</h1>

<p align="center">
  Decentralized network for smart storage
</p>

<p align="center">      
    <a href="https://github.com/phantasma-io/PhantasmaChain/workflows/.NET%20Core/badge.svg?branch=master">
        <img src="https://github.com/phantasma-io/PhantasmaChain/workflows/.NET%20Core/badge.svg">
    </a>
    <a href="https://github.com/phantasma-io/PhantasmaChain/blob/master/LICENSE">
        <img src="https://img.shields.io/badge/license-MIT-blue.svg">
    </a>
    <a href="https://discord.gg/RsKn8EN">
        <img src="https://img.shields.io/discord/404769727634997261.svg">
    </a>
    <a href="https://twitter.com/phantasmachain">
        <img src="https://img.shields.io/twitter/follow/phantasmachain.svg?style=social">
    </a>
</p>

<p align="center">
    <a href="">
        <img src="https://img.shields.io/github/last-commit/phantasma-io/phantasma-ng.svg?style=flat">
    </a>
    <a href="">
        <img src="https://img.shields.io/github/commit-activity/y/phantasma-io/phantasma-ng.svg?style=flat">
    </a>
    <a href="https://github.com/phantasma-io/phantasma-ng">
        <img src="https://tokei.rs/b1/github/phantasma-io/phantasma-ng">
    </a>
</p>

## Contents

- [Description](#description)
- [Components](#components)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Building](#building)
- [Contributing](#contributing)
- [License](#license)

---

## Description

Phantasma implements a decentralized content distribution system running on the blockchain, with strong emphasis on privacy and security.

To learn more about Phantasma, please read the [White Paper](https://phantasma.io/phantasma_whitepaper.pdf).

## Components

Component	| Description	| Status	| Percentage
:---------------------- | :------------ | :------------  | :------------ 
Chain Core 		| eg: accounts, transactions, blocks | In development | 95%
Wallet | CLI wallet | In development | 95%
VM 		| Virtual machine to run smart contracts | In development | 95%
Smart Contracts | eg: language features, compilers | In development | 80%
Economy | Tokens / NFT | In development | 95%
Network 			| P2P communication | In development | 85%
Consensus | Distributed consensus for nodes | In development | 85%
Scalabilty | Side-chains / Channels | In development | 80%
Relay | Off-chain relay | In development | 60%
Storage | Distributed storage | In development| 90%
API 			| RPC api for nodes | In development | 90%

## Compatibility

Platform 		| Status
:---------------------- | :------------
.NET Framework 		| Working
.NET Core 		| Working
Unity 			| Working
Xamarin / Mobile 	| Working
C++ 		| Working
Mono 			| Working
UWP 			| Untested

## Installation

To install Phantasma SDK to your project, run the following command in the [Package Manager Console](https://docs.nuget.org/ndocs/tools/package-manager-console):

```
PM> Install-Package Phantasma
```

## Building

To build Phantasma on Windows, you need to download [Visual Studio 2017](https://www.visualstudio.com/products/visual-studio-community-vs), install the [.NET Framework 4.7 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=55168) and the [.NET Core SDK](https://www.microsoft.com/net/core).

If you need to develop on Linux or macOS, just install the [.NET Core SDK](https://www.microsoft.com/net/core).

For more information about how to build dApps for Phantasma, please read the [documentation](http://phantasma.io/development).

## Debuging

To effectively debug `Phantasma.Node`, follow the steps outlined below:

1. **Tendermint Executable**:
    - To debug `Phantasma.Node`, it's essential to have the `tendermint` executable.
    - Download it from this URL: [Tendermint v0.34.21 Release](https://github.com/tendermint/tendermint/releases/tag/v0.34.21).
    - Place the downloaded executable inside the path: `/path/to/phantasma-ng/Phantasma.Node/src/bin/Debug/net6.0/tendermintFile`.

2. **Configuration Settings**:
    - In the `config.json` file, ensure that you specify the path to the folder mentioned above and the `tendermint` executable.
    - You can find an example configuration in the `Phantasma.Node/src` directory.

3. **Reset the Blockchain**:
    - Delete the `Storage` folder.
    - Remove the `Storage` folder located at `/path/to/phantasma-ng/Phantasma.Node/src/bin/Debug/net6.0/`.
    - Remove the `data` folder found inside `/path/to/phantasma-ng/Phantasma.Node/src/bin/Debug/net6.0/tendermintFile`.
    - Navigate to the `DOCKER` folder using a terminal and execute the script: `./fix-storage.sh`. This will reset the blockchain.
    - An easy way to do the deployment and reset in on step is to run this command: `cd DOCKER && ./fix-storage.sh || cd .. && ./testnet-startup.sh`.
4. **Edit Necessary Files for Protocol Version**:
    - In `Phantasma.Business/src/Blockchain/Nexus/Nexus.cs`:
        - Update the value of `DomainSettings.Phantasma30Protocol` to `DomainSettings.LatestKnownProtocol` at lines 937, 961, and 978.
    - In `Phantasma.Node/src/ABCIConnector.cs`:
        - Update the value on line 420 from `uint version = DomainSettings.Phantasma30Protocol;` to `uint version = DomainSettings.LatestKnownProtocol;`.

5. **Run Node in Editor**:
    - If you intend to run the node in an editor like Rider or Visual Studio for debugging purposes:
        - Edit the `DOCKER/wrapper-testnet.sh` file.
        - Comment out line 28.

## Contributing

You can contribute to Phantasma with [issues](https://github.com/Phantasma-io/PhantasmaChain/issues) and [PRs](https://github.com/Phantasma-io/PhantasmaChain/pulls). Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## License

[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/phantasma-io/phantasma-ng/blob/master/LICENSE)

The Phantasma project is released under the MIT license, see `LICENSE.md` for more details.

## Related Projects

Project	| Description	| Status	| Percentage
:---------------------- | :------------ | :------------  | :------------ 
[Phantasma Wallet](https://www.phantasma.io/wallets) 		| Cross platform Phantasma wallet | 2 Available | 100%
[Phantasma Explorer](https://explorer.phantasma.io/) | Block explorer for visualizing Phantasma chain data | Complete | 100%
[Phantasma Node](https://github.com/phantasma-io/PhantasmaNode) | Phantasma node deployment | In development | 90%
[Phantasma SDK](https://github.com/phantasma-io/PhantasmaSDK) 		| Software development kit | In development | 60%
[Phantasma Link](https://github.com/phantasma-io/PhantasmaLink) 		| Browser extensions for interacting with Phantasma dApps | Complete - PhantasmaLink | 100%
[Phantasma Compiler](https://github.com/phantasma-io/PhantasmaCompiler) | Phantasma smart contract compiler for high-level languages | In development | 30%
[Phantasma Assembler](https://github.com/phantasma-io/PhantasmaAssembler) | Phantasma smart contract assembler for low-level instructions | In development | 90%
[Nachomen](https://nacho.men) 			| Phantasma-based video game | In development | 95%
