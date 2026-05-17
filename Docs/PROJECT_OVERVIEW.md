# Emblem Project Overview

## Project Summary

This is a turn-based tactical RPG game developed using Godot 4.x engine with C# (.NET 8.0). The project is inspired by the Fire Emblem series (specifically FE5), featuring classic tactical gameplay mechanics.

**Engine Version**: Godot 4.x  
**Programming Language**: C# (.NET 8.0)  
**Project Type**: 2D Tactical RPG Game  
**Created Date**: 2026-05-17  
**Last Updated**: 2026-05-17

---

## Directory Structure

```
Emblem/
├── .godot/                    # Auto-generated Godot engine files
├── .vscode/                   # VSCode editor configuration
│   ├── launch.json            # Debug launch configuration
│   └── tasks.json             # Build task configuration
├── Scenes/                    # Game scene resources
│   └── Main.tscn              # Main game scene
├── Scripts/                   # C# source code
│   ├── Combat/                # Combat system module
│   ├── Core/                  # Core system module
│   ├── Map/                   # Map system module
│   ├── Test/                  # Testing module
│   └── Units/                 # Unit system module
├── Documentation/             # Project documentation
│   └── FE5_Godot_Implementation.md  # FE5 implementation reference
├── .editorconfig              # Editor configuration
├── .gitattributes             # Git attributes
├── .gitignore                 # Git ignore rules
├── Emblem.csproj              # C# project file
├── Emblem.sln                 # Solution file
├── icon.svg                   # Project icon
└── project.godot              # Godot project configuration
```

---

## Module Breakdown

### 1. Combat System (Combat/)

Handles all battle logic and combat flow management.

| File | Description |
|------|-------------|
| `BattleManager.cs` | Manages battle flow, handles battle start/process/end |
| `BattleResult.cs` | Data structure storing battle outcome and stat changes |
| `CombatCalculator.cs` | Calculates damage, hit chance, critical rate, etc. |
| `ExperienceManager.cs` | Experience point calculation and management (interfaces only) |

### 2. Core System (Core/)

Game framework and flow control.

| File | Description |
|------|-------------|
| `TurnManager.cs` | Controls player/enemy turn switching |
| `TurnPhase.cs` | Enum defining turn phases (preparation, action, end) |

### 3. Map System (Map/)

Map rendering, grid management, and user interaction.

| File | Description |
|------|-------------|
| `MapManager.cs` | Loads and manages map data |
| `MapRenderer.cs` | Renders map visually |
| `Pathfinder.cs` | A* pathfinding algorithm implementation |
| `UserInputHandler.cs` | Handles mouse/touch input |

### 4. Unit System (Units/)

Character/unit attributes and management.

| File | Description |
|------|-------------|
| `Unit.cs` | Base unit class with common properties and behaviors |
| `UnitStats.cs` | Stores combat stats (HP, attack, defense, etc.), includes growth system |
| `BaseStats.cs` | Standalone base stats class for encapsulation |
| `GrowthManager.cs` | Handles level-up stat growth calculation |
| `LevelUpResult.cs` | Data structure for level-up results (inherits Godot RefCounted) |

### 5. Testing Module (Test/)

| File | Description |
|------|-------------|
| `TestRunner.cs` | Executes unit tests and integration tests |
| `TestGrowthSystem.cs` | Unit tests for the character growth system |

---

## Configuration Files

### project.godot
Godot engine configuration including project settings, input maps, and display settings.

### Emblem.csproj / Emblem.sln
C# project and solution files for .NET compilation and reference management.

### .editorconfig
Enforces consistent code style across the development team.

---

## Development Environment

### Recommended IDE
- **VSCode** + C# Dev Kit extension
- **Visual Studio 2022+**

### Debug Configuration
VSCode debug launch configuration is available in `.vscode/launch.json`.

---

## Development Roadmap

| Status | Task | Priority |
|--------|------|----------|
| [ ] | Implement battle animations | High |
| [ ] | Add class system | High |
| [ ] | Implement weapon system | High |
| [ ] | Develop level editor | Medium |
| [ ] | Add save/load functionality | Medium |
| [ ] | Implement terrain effects | Medium |
| [ ] | Add UI interface | High |

---

## Project Progress Summary

### Current Status
- Core framework established
- Basic turn system implemented
- Map rendering and pathfinding functional
- Basic combat calculation completed
- Character attribute system implemented (BaseStats, UnitStats)
- Level and experience system implemented (Level 1-20, 100 EXP per level)
- Growth rate system implemented (independent growth per stat)
- Weapon proficiency system implemented (9 weapon types, E-S ranks)
- Experience calculation interfaces designed (algorithms pending implementation)

### Next Milestone
Complete battle system implementation with animations and visual feedback.

---

*Document maintained by: Emblem Development Team*
*Generated: 2026-05-17*
