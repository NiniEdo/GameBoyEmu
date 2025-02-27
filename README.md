#A C# GameBoy emulator

My first emulator, written just for fun.

GameBoyEmu is a Game Boy emulator written in C# targeting .NET 8. The emulator aims to simulate the Nintendo Game Boy hardware, allowing users to run Game Boy ROMs on their PC.
### Features
- CPU Emulation: Emulates the processor used in the Game Boy and passes blarg's tests.
- Memory Management: Handles memory mapping and management, including ROM, RAM, and I/O registers.
- Cartridge Support: Loads and reads metadata from Game Boy cartridge files (.gb).
- Timer Implementation: Accurately emulates the Game Boy's timer functionality, including the DIV, TIMA, TMA, and TAC registers.
- Interrupt Handling: Manages hardware interrupts, including timer interrupts.
- Logging: Utilizes NLog for detailed logging and debugging.

### Prerequisites
-	.NET 8 SDK: Ensure that you have the .NET 8 SDK installed on your system.
-	Visual Studio: Recommended IDE for development and debugging.

### Installation
1.	Clone the repository:

```bash
git clone https://github.com/yourusername/GameBoyEmu.git
```

2.	Build the project:
```bash
dotnet build
```
### Running the Emulator
1.	Place ROM files:
Copy your Game Boy ROM files (.gb extension) into the root directory of the project.
2.	Run the emulator:
```bash
dotnet run
```
### Dependencies
-	NLog (v5.3.4): Used for logging emulator operations and debugging.
-	Sayers.SDL2.Core (v1.0.11): Planned for handling graphics and input (implementation in progress).

### What's missing
- Graphics Output: Graphics rendering does not work properly and need fixing.
- Limited MBC Support: Memory Bank Controllers (MBC) are not fully supported.
- PPU not implemented

### License
This project is licensed under the MIT License.
