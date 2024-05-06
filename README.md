# Paragon Pioneers 2 Production Stats

This is a [BepInEx](https://github.com/BepInEx/BepInEx) plugin for [Paragon Pioneers 2](https://paragonpioneers.com/) that aims to show some more detailed resource and unit production and consumption statistics.

## Current features

- **Resource production/consumption.** Some text is added to the island storage screen detailing how much of a given item can be produced and consumed on the current island and by what (see image below). Statistics are collected from the following sources (Note that resource consumption by palaces is not shown.):

  - Resource production buildings (buildings that harvest crops or trees or gather resources from multiple fields also take into account the number of available fields);
  - Unit production buildings;
  - Population need buildings that consume resources;
  - Resource doubling buildings;
  - Population.

![Production statistics example](img/production_stats_example.png "Production statistics example")

- **Unit production/consumption.** Some text is added to the island garrison screen showing how much of the selected unit is being produced and consumed. Statistics are collected from the following sources:
  - Unit production buildings;
  - Population.

## Installation

**Note:** The mod has only been tested on the Paragon Pioneers 2 Steam version on Windows, with BepInEx installed manually. However, there are mod loaders that can make installation easier.

1. Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html) version 5.x;
2. Download the latest mod version from Releases;
3. Place the `PP2ProductionStats.dll` file in the `BepInEx/plugins` directory.

## Reporting bugs

If you encounter a bug or crash with the plugin installed, please [create an issue](https://github.com/kepons/pp2-production-stats/issues/new) with a screenshot or text of your error.

**Please do not report crashes that contain `PP2ProductionStats` in the error text to the Paragon Pioneers 2 developer because that is a problem with the plugin and not the game itself. File an issue here instead. If you do report an error to the developer, please mention if you are using any mods.**

## Development

Requirements:

- .NET SDK.

To build the project locally, you will need to point the project to some game DLLs. Instructions can be found as comments in the project file `PP2ProductionStats.csproj`.