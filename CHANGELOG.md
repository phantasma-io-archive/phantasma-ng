# Phantasma Blockchain Changelog
All notable changes to this project will be documented in this file.

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