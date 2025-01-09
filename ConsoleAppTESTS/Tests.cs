using GameBoyEmu.CpuNamespace;
using GameBoyEmu.MemoryNamespace;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using NLog;

namespace ConsoleAppTESTS
{
    internal class Tests
    {
        string[] files;
        TestMemory mem;
        Cpu cpu;
        private Logger _logger = LogManager.GetCurrentClassLogger();

        public Tests()
        {
            files = Directory.GetFiles(@"..\\..\\..\\tests\\sm83\\v1");

            mem = new TestMemory();
            cpu = new Cpu(mem);
        }

        public void Start()
        {
            foreach (string filePath in files)
            {
                string fileContent = File.ReadAllText(filePath);
                JsonNode? jsonObject = JsonNode.Parse(fileContent);

                if (jsonObject is JsonArray jsonArray)
                {
                    foreach (JsonObject obj in jsonArray)
                    {
                        string name = obj["name"]!.GetValue<string>();

                        var initial = obj["initial"] as JsonObject;
                        if (initial != null)
                        {
                            cpu.AF[1] = initial["a"]!.GetValue<byte>();
                            cpu.BC[0] = initial["c"]!.GetValue<byte>();
                            cpu.BC[1] = initial["b"]!.GetValue<byte>();
                            cpu.DE[0] = initial["e"]!.GetValue<byte>();
                            cpu.DE[1] = initial["d"]!.GetValue<byte>();
                            cpu.AF[0] = initial["f"]!.GetValue<byte>();
                            cpu.HL[0] = initial["l"]!.GetValue<byte>();
                            cpu.HL[1] = initial["h"]!.GetValue<byte>();
                            ushort pcValue = initial["pc"]!.GetValue<ushort>();
                            ushort spValue = initial["sp"]!.GetValue<ushort>();
                            cpu.PC[0] = (byte)(pcValue & 0xFF);       
                            cpu.PC[1] = (byte)((pcValue >> 8) & 0xFF); 
                            cpu.SP[0] = (byte)(spValue & 0xFF);       
                            cpu.SP[1] = (byte)((spValue >> 8) & 0xFF); 
                            mem[0xFFFF] = initial["ie"]!.GetValue<byte>();
                            cpu.ImeFlag = initial["ime"]!.GetValue<byte>() == 1;
                            if (initial["ram"] is JsonArray ramArray)
                            {
                                for (int i = 0; i < ramArray.Count; i++)
                                {
                                    JsonArray ramEntry = (JsonArray)ramArray[i];
                                    if (ramEntry[0] != null && ramEntry[1] != null)
                                    {
                                        ushort address = ramEntry[0]!.GetValue<ushort>();
                                        byte value = ramEntry[1]!.GetValue<byte>();
                                        mem[address] = value;
                                    }
                                }
                            }
                        }

                        _logger.Info($"Test {name}");
                        cpu.KeepRunning = false;
                        cpu.Execute();

                        var final = obj["final"] as JsonObject;
                        byte[] AF;
                        if (final != null)
                        {
                            bool A = cpu.AF[1] == final["a"]!.GetValue<byte>();
                            bool C = cpu.BC[0] == final["c"]!.GetValue<byte>();
                            bool B = cpu.BC[1] == final["b"]!.GetValue<byte>();
                            bool E = cpu.DE[0] == final["e"]!.GetValue<byte>();
                            bool D = cpu.DE[1] == final["d"]!.GetValue<byte>();
                            bool F = cpu.AF[0] == final["f"]!.GetValue<byte>();
                            bool L = cpu.HL[0] == final["l"]!.GetValue<byte>();
                            bool H = cpu.HL[1] == final["h"]!.GetValue<byte>();
                            bool PC = BitConverter.ToUInt16(cpu.PC, 0) == final["pc"]!.GetValue<ushort>();
                            bool SP = BitConverter.ToUInt16(cpu.SP, 0) == final["sp"]!.GetValue<ushort>();
                            bool IME = cpu.ImeFlag == (final["ime"]!.GetValue<byte>() == 1);
                            bool RAM = true;

                            if (final["ram"] is JsonArray finalRamArray)
                            {
                                for (int i = 0; i < finalRamArray.Count; i++)
                                {
                                    JsonArray ramEntry = (JsonArray)finalRamArray[i];
                                    if (ramEntry[0] != null && ramEntry[1] != null)
                                    {
                                        ushort address = ramEntry[0]!.GetValue<ushort>();
                                        byte value = ramEntry[1]!.GetValue<byte>();
                                        if (mem[address] != value)
                                        {
                                            RAM = false;
                                            break;
                                        }
                                    }
                                }
                            }

                            bool allTestsPassed = A && C && B && E && D && F && L && H && PC && SP && IME && RAM;
                            _logger.Info($"Test {name}: {(allTestsPassed ? "Passed" : "Failed")}");
                            if (!allTestsPassed)
                            {
                                _logger.Info($"Expected A: {final["a"]!.GetValue<byte>()}, Got: {cpu.AF[1]}");
                                _logger.Info($"Expected C: {final["c"]!.GetValue<byte>()}, Got: {cpu.BC[0]}");
                                _logger.Info($"Expected B: {final["b"]!.GetValue<byte>()}, Got: {cpu.BC[1]}");
                                _logger.Info($"Expected E: {final["e"]!.GetValue<byte>()}, Got: {cpu.DE[0]}");
                                _logger.Info($"Expected D: {final["d"]!.GetValue<byte>()}, Got: {cpu.DE[1]}");
                                _logger.Info($"Expected F: {final["f"]!.GetValue<byte>()}, Got: {cpu.AF[0]}");
                                _logger.Info($"Expected L: {final["l"]!.GetValue<byte>()}, Got: {cpu.HL[0]}");
                                _logger.Info($"Expected H: {final["h"]!.GetValue<byte>()}, Got: {cpu.HL[1]}");
                                _logger.Info($"Expected PC: {final["pc"]!.GetValue<ushort>()}, Got: {BitConverter.ToUInt16(cpu.PC, 0)}");
                                _logger.Info($"Expected SP: {final["sp"]!.GetValue<ushort>()}, Got: {BitConverter.ToUInt16(cpu.SP, 0)}");
                                _logger.Info($"Expected IME: {final["ime"]!.GetValue<byte>() == 1}, Got: {cpu.ImeFlag}");
                                _logger.Info($"RAM: {RAM}");
                                return;
                            }
                        }

                    }
                }
            }
        }
    }
}
