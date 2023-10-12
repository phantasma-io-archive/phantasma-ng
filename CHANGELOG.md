# Phantasma Blockchain Changelog
All notable changes to this project will be documented in this file.

## Version 17 - 13 October, 2023
### Added

### Changed
- Bumped version to 17.
- All of the changes of version 16 moved to version 17.
### Fixed

## Version 16 - 12 October, 2023
### Added
- Added `docker-compose.yml` file to run the Phantasma Blockchain in a Docker container.
- Added `docker-entrypoint.sh` file to run the Phantasma Blockchain in a Docker container.
- Added `DockerfileTestnetDebug` file to help debug the Phantasma Blockchain in a Docker container.
- Added `DockerfileTestnetNodeBuilder` file to run the Phantasma Blockchain in a Docker container.
- Added `DockerfileNodeWin` file to build the Phantasma Blockchain.
- Added `wrapper-testnet-debug.sh` file to help debug the Phantasma Blockchain in a Docker container.
- Added a way to provide the `config.json` file as a parameter on startup.

### Changed
- Bumped version to 16.
- Upgrade to Simnet to increase the amount of SOUL and KCAL the user receives (Development only).
- Updated `Readme.md` file to include instructions on how to debug the Phantasma Blockchain.
- Updated `ABCIConnector.cs` file to check the CurrentBlock is null.

### Fixed
- Bug fixes to the ConsensusContract.
- Changed `Oracle.cs` to access directly the storage.
- Fixed `NexusValidator.cs` to get the validators from the storage.

## Version 15 - March 30, 2023
### Added

### Changed
- Bumped version to 15.

### Fixed


## Version 14 - March 10, 2023
### Added
- Added `CHANGELOG.md` file to track changes.
- Added `CONTRIBUTING.md` file to describe how to contribute to the project.
- Added `LICENSE` file to describe the license of the project.
- Added `CODE_OF_CONDUCT.md` file to describe the code of conduct of the project.
- Added costs to create a DAO (Decentralized Autonomous Organization).

### Changed
- Bumped version to 14.
- Improved consensus mechanisms for faster transaction processing.
- Improved ConsensusContract everyone can vote depending on the organization (DAO) selected for the Poll.
- Improvements to ValidatorContract (this will allow the demotion / activity registration of the Validators).
- File structer improvements, now the project is more organized (Still needs improvements).
- Updated the Chain.cs and Nexus.cs (To Check the Validators and register their activity).

### Fixed
- Bug fixes and improvements made to the DEX (ExchangeContract).
- Bug fixes to the ScriptContext.
- Bug fixes to Extcalls.cs (Validations and security improvements).
- Bug fixes to Block Rewards calculation and distribution. (Now the rewards are distributed to the Validators instead of the BlockContract).
- Bug fix Filters Spam.
- Bug fix the Cosmic Swaps (Some issues with cosmic swaps).
- Bug fix the LPContract.