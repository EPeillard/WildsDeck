# HunterPie reference audit

Inspected upstream: HunterPie commit [`ef654889658684848cb465176b676b9b553ea102`](https://github.com/HunterPie/HunterPie/commit/ef654889658684848cb465176b676b9b553ea102), dated 2026-08-14. Its newest Wilds map at inspection time is `MonsterHunterWilds.1.42.0.2.map`.

Paths below are relative to the HunterPie repository. Confidence describes how directly the WildsDeck field follows current upstream code, not whether future patches preserve the layout.

| WildsDeck field | HunterPie reference | Map symbol(s) | Confidence |
|---|---|---|---|
| process version/map | `.../Process/MHWildsProcessAttachStrategy.cs` | exact filename | High |
| address parser | `HunterPie.Core/Address/Map/Internal/LegacyAddressMapParser.cs` | `Address`, `Offset` | High |
| pointer traversal | `HunterPie.Platforms/Windows/Memory/WindowsMemory.cs` | all paths | High |
| quest active/mode | `.../Entity/Game/MHWildsGame.cs::GetQuestAsync`; `Definitions/Quest/MHWildsCurrentQuestInformation.cs` | `Game::QuestManager`, `Quest::Data`, `Quest::CurrentInformation`, `Quest::IsRetrying` | High |
| monster enumeration | `.../Entity/Game/MHWildsGame.cs::GetMonstersAsync`; `MHWildsMonsterBasicData.cs` | `Game::EnemyManager`, `Environment::MonsterList`, `Monster::Magic`, `Monster::BasicData` | High |
| camera target | `.../Entity/Enemy/MHWildsMonster.cs::GetTargetAsync` | `Game::CameraManager`, `Camera::Monster::Target`, `Monster::Context` | High |
| monster health | `MHWildsMonster.cs::GetHealthAsync`; `MHWildsMonsterHealth.cs` | `Monster::Health` | High |
| encrypted floats | `Entity/Crypto/MHWildsCryptoService.cs`, `ManualAesCrypto.cs`, `MHWildsEncryptedFloat.cs` | `Encryption::Key`, `Encryption::Round` | High |
| enrage | `MHWildsMonster.cs::GetEnrageAsync`; `MHWildsAilment.cs`; `MHWildsBuildUp.cs` | `Monster::Enrage` | High |
| stamina | `MHWildsMonster.cs::GetStaminaAsync` | `Monster::Stamina` | High |
| capture threshold | `MHWildsMonster.cs::GetThresholdsAsync`; UI `MonsterContextHandler.cs` comparison | `Monster::Thresholds` | High |
| parts | `MHWildsMonster.cs::GetPartsAsync`; `MHWildsMonsterPartData.cs`; `MHWildsPartHealth.cs`; `MHWildsPartBreak.cs` | `Monster::Parts` | Medium: collection/progress direct, labels incomplete |
| ailments | `MHWildsMonster.cs::GetAilmentsAsync`; `Game/Wilds/Data/MonsterData.xml` | `Monster::Ailments` | Medium: IDs direct, some labels unknown upstream |
| player name/HR | `.../Entity/Player/MHWildsPlayer.cs::GetBasicDataAsync`; `MHWildsPlayerContext.cs` | `Game::PlayerManager`, `Player::Local`, `Player::Context`, `Save::Player::HunterRank` | High |
| weapon | `MHWildsPlayer.cs::GetWeaponAsync`; `MHWildsPlayerGearContext.cs` | `Player::Gear` | High |
| attack/affinity | `MHWildsPlayer.cs::GetStatusAsync`; `MHWildsDamageStatus.cs`; `MHWildsAffinityStatus.cs` | `Player::Status::Damage`, `Player::Status::Affinity` | High |
| local/party damage | `MHWildsPlayer.cs::GetPartyAsync`, `GetDamageByPlayerAsync`, `GetQuestSynced*Damage` | `Quest::LocalPlayer::Damage`, `Quest::RemotePlayer::Damage`, party symbols | Medium: synchronized values direct; remote identity incomplete in WildsDeck |
| Support Ship | `MHWildsPlayer.cs::GetSupportShipAsync`; `MHWildsSupportShipContext.cs` | `Activities::SupportShip` | High |
| Ingredients Center | `MHWildsPlayer.cs::GetIngredientsCenterAsync`; `MHWildsIngredientCenterContext.cs` | `Activities::IngredientsCenter` | High |
| Material Retrieval | `MHWildsPlayer.cs::GetMaterialRetrievalAsync`; collector definitions | `Activities::MaterialRetrieval` | Medium: WildsDeck aggregates upstream collector slots |
| NPC party members | `MHWildsPlayer.cs::GetNpcPartyMembersAsync`; party definitions | `Game::NpcManager`, `Npcs::Party`, `Npc::DamageHistory` | High for hunt party NPCs; not a town alert source |
| town NPC alerts | No reliable generic town notification data source found in current Wilds integration | `Game::NpcManager` alone is insufficient | Unsupported |

## Deliberate divergence

HunterPie's general Windows process layer currently requests `PROCESS_ALL_ACCESS` and contains write/injection APIs used elsewhere in that application. WildsDeck did **not** reuse that layer. `ProcessMemoryReader` declares only read/query access and contains no write, protection, allocation, remote-thread, or injection P/Invoke.

