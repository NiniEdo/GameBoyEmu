using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NLog;
using GameBoyEmu.Exceptions;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.CompilerServices;

namespace GameBoyEmu.CartridgeNamespace
{
    internal class Cartridge
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private byte[]? _romDump;
        private readonly Dictionary<string, string> _newLicenseeCodes = new Dictionary<string, string>
        {
            { "00", "None" },
            { "01", "Nintendo Research & Development 1" },
            { "08", "Capcom" },
            { "13", "EA (Electronic Arts)" },
            { "18", "Hudson Soft" },
            { "19", "B-AI" },
            { "20", "KSS" },
            { "22", "Planning Office WADA" },
            { "24", "PCM Complete" },
            { "25", "San-X" },
            { "28", "Kemco" },
            { "29", "SETA Corporation" },
            { "30", "Viacom" },
            { "31", "Nintendo" },
            { "32", "Bandai" },
            { "33", "Ocean Software/Acclaim Entertainment" },
            { "34", "Konami" },
            { "35", "HectorSoft" },
            { "37", "Taito" },
            { "38", "Hudson Soft" },
            { "39", "Banpresto" },
            { "41", "Ubi Soft1" },
            { "42", "Atlus" },
            { "44", "Malibu Interactive" },
            { "46", "Angel" },
            { "47", "Bullet-Proof Software2" },
            { "49", "Irem" },
            { "50", "Absolute" },
            { "51", "Acclaim Entertainment" },
            { "52", "Activision" },
            { "53", "Sammy USA Corporation" },
            { "54", "Konami" },
            { "55", "Hi Tech Expressions" },
            { "56", "LJN" },
            { "57", "Matchbox" },
            { "58", "Mattel" },
            { "59", "Milton Bradley Company" },
            { "60", "Titus Interactive" },
            { "61", "Virgin Games Ltd.3" },
            { "64", "Lucasfilm Games4" },
            { "67", "Ocean Software" },
            { "69", "EA (Electronic Arts)" },
            { "70", "Infogrames5" },
            { "71", "Interplay Entertainment" },
            { "72", "Broderbund" },
            { "73", "Sculptured Software6" },
            { "75", "The Sales Curve Limited7" },
            { "78", "THQ" },
            { "79", "Accolade" },
            { "80", "Misawa Entertainment" },
            { "83", "lozc" },
            { "86", "Tokuma Shoten" },
            { "87", "Tsukuda Original" },
            { "91", "Chunsoft Co.8" },
            { "92", "Video System" },
            { "93", "Ocean Software/Acclaim Entertainment" },
            { "95", "Varie" },
            { "96", "Yonezawa/s’pal" },
            { "97", "Kaneko" },
            { "99", "Pack-In-Video" },
            { "9H", "Bottom Up" },
            { "A4", "Konami (Yu-Gi-Oh!)" },
            { "BL", "MTO" },
            { "DK", "Kodansha" }
        };
        private readonly Dictionary<string, string> _oldLicenseeCodes = new Dictionary<string, string>
        {
            { "00", "None" },
            { "01", "Nintendo" },
            { "08", "Capcom" },
            { "09", "HOT-B" },
            { "0A", "Jaleco" },
            { "0B", "Coconuts Japan" },
            { "0C", "Elite Systems" },
            { "13", "EA (Electronic Arts)" },
            { "18", "Hudson Soft" },
            { "19", "ITC Entertainment" },
            { "1A", "Yanoman" },
            { "1D", "Japan Clary" },
            { "1F", "Virgin Games Ltd.3" },
            { "24", "PCM Complete" },
            { "25", "San-X" },
            { "28", "Kemco" },
            { "29", "SETA Corporation" },
            { "30", "Infogrames5" },
            { "31", "Nintendo" },
            { "32", "Bandai" },
            { "33", "Indicates that the New licensee code should be used instead." },
            { "34", "Konami" },
            { "35", "HectorSoft" },
            { "38", "Capcom" },
            { "39", "Banpresto" },
            { "3C", "Entertainment Interactive (stub)" },
            { "3E", "Gremlin" },
            { "41", "Ubi Soft1" },
            { "42", "Atlus" },
            { "44", "Malibu Interactive" },
            { "46", "Angel" },
            { "47", "Spectrum HoloByte" },
            { "49", "Irem" },
            { "4A", "Virgin Games Ltd.3" },
            { "4D", "Malibu Interactive" },
            { "4F", "U.S. Gold" },
            { "50", "Absolute" },
            { "51", "Acclaim Entertainment" },
            { "52", "Activision" },
            { "53", "Sammy USA Corporation" },
            { "54", "GameTek" },
            { "55", "Park Place13" },
            { "56", "LJN" },
            { "57", "Matchbox" },
            { "59", "Milton Bradley Company" },
            { "5A", "Mindscape" },
            { "5B", "Romstar" },
            { "5C", "Naxat Soft14" },
            { "5D", "Tradewest" },
            { "60", "Titus Interactive" },
            { "61", "Virgin Games Ltd.3" },
            { "67", "Ocean Software" },
            { "69", "EA (Electronic Arts)" },
            { "6E", "Elite Systems" },
            { "6F", "Electro Brain" },
            { "70", "Infogrames5" },
            { "71", "Interplay Entertainment" },
            { "72", "Broderbund" },
            { "73", "Sculptured Software6" },
            { "75", "The Sales Curve Limited7" },
            { "78", "THQ" },
            { "79", "Accolade15" },
            { "7A", "Triffix Entertainment" },
            { "7C", "MicroProse" },
            { "7F", "Kemco" },
            { "80", "Misawa Entertainment" },
            { "83", "LOZC G." },
            { "86", "Tokuma Shoten" },
            { "8B", "Bullet-Proof Software2" },
            { "8C", "Vic Tokai Corp.16" },
            { "8E", "Ape Inc.17" },
            { "8F", "I’Max18" },
            { "91", "Chunsoft Co.8" },
            { "92", "Video System" },
            { "93", "Tsubaraya Productions" },
            { "95", "Varie" },
            { "96", "Yonezawa19/S’Pal" },
            { "97", "Kemco" },
            { "99", "Arc" },
            { "9A", "Nihon Bussan" },
            { "9B", "Tecmo" },
            { "9C", "Imagineer" },
            { "9D", "Banpresto" },
            { "9F", "Nova" },
            { "A1", "Hori Electric" },
            { "A2", "Bandai" },
            { "A4", "Konami" },
            { "A6", "Kawada" },
            { "A7", "Takara" },
            { "A9", "Technos Japan" },
            { "AA", "Broderbund" },
            { "AC", "Toei Animation" },
            { "AD", "Toho" },
            { "AF", "Namco" },
            { "B0", "Acclaim Entertainment" },
            { "B1", "ASCII Corporation or Nexsoft" },
            { "B2", "Bandai" },
            { "B4", "Square Enix" },
            { "B6", "HAL Laboratory" },
            { "B7", "SNK" },
            { "B9", "Pony Canyon" },
            { "BA", "Culture Brain" },
            { "BB", "Sunsoft" },
            { "BD", "Sony Imagesoft" },
            { "BF", "Sammy Corporation" },
            { "C0", "Taito" },
            { "C2", "Kemco" },
            { "C3", "Square" },
            { "C4", "Tokuma Shoten" },
            { "C5", "Data East" },
            { "C6", "Tonkin House" },
            { "C8", "Koei" },
            { "C9", "UFL" },
            { "CA", "Ultra Games" },
            { "CB", "VAP, Inc." },
            { "CC", "Use Corporation" },
            { "CD", "Meldac" },
            { "CE", "Pony Canyon" },
            { "CF", "Angel" },
            { "D0", "Taito" },
            { "D1", "SOFEL (Software Engineering Lab)" },
            { "D2", "Quest" },
            { "D3", "Sigma Enterprises" },
            { "D4", "ASK Kodansha Co." },
            { "D6", "Naxat Soft14" },
            { "D7", "Copya System" },
            { "D9", "Banpresto" },
            { "DA", "Tomy" },
            { "DB", "LJN" },
            { "DD", "Nippon Computer Systems" },
            { "DE", "Human Ent." },
            { "DF", "Altron" },
            { "E0", "Jaleco" },
            { "E1", "Towa Chiki" },
            { "E2", "Yutaka # Needs more info" },
            { "E3", "Varie" },
            { "E5", "Epoch" },
            { "E7", "Athena" },
            { "E8", "Asmik Ace Entertainment" },
            { "E9", "Natsume" },
            { "EA", "King Records" },
            { "EB", "Atlus" },
            { "EC", "Epic/Sony Records" },
            { "EE", "IGS" },
            { "F0", "A Wave" },
            { "F3", "Extreme Entertainment" },
            { "FF", "LJN" }
        };

        //TODO: Implement switchable rom and mbc
        public Cartridge() { }

        public byte[] LoadRomFromCartridge()
        {
            string directoryPath = @"..\..\..";
            String[] files = Directory.GetFiles(directoryPath, "*.gb");
            String? bgFile = files.FirstOrDefault();

            if (bgFile == null)
            {
                throw new CartridgeException("Cartrige not found");
            }

            try
            {
                _romDump = File.ReadAllBytes(bgFile);
                _logger.Info($"Loaded cartridge: {bgFile.Replace("..\\..\\..\\", "")}");
            }
            catch (IOException IOex)
            {
                throw new CartridgeException("Failed to read cartridge: " + IOex.Message);
            }

            ReadHeaderData();

            return _romDump;
        }
        private void ReadHeaderData()
        {
            if (_romDump == null || _romDump.Length < 0x144)
            {
                throw new CartridgeException("ROM data is incomplete or corrupted");
            }

            CheckOriginalCartridge();
            ReadMetadata();
            HeaderChecksum();
            ReadCartridgeMBC();
            //global checksum skipped because not mandatory
        }

        private void CheckOriginalCartridge()
        {
            byte[] logo = {
                0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
                0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
                0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E
            };

            byte[] logoDump = _romDump![0x104..0x134];

            bool isOriginal = logo.SequenceEqual(logoDump);

            if (isOriginal)
            {
                _logger.Info("Logo dump OK, the cartridge is original!");
            }
            else
            {
                _logger.Warn("Logo dump failed, the cartridge is fake!");
            }
        }

        private void ReadMetadata()
        {
            //0134-0143 — Title or 013F-0142 — Manufacturer code
            byte[] romMetadata = _romDump![0x134..0x144];

            if (romMetadata[romMetadata.Length - 1] == 0xC0)
            {
                throw new CartridgeException("This game is for CGB only");
            }

            if (romMetadata[romMetadata.Length - 1] == 0x80) // new metadata informations format
            {
                byte[] titleDump = _romDump[0x134..0x13F];
                string title = Encoding.ASCII.GetString(titleDump);

                byte[] manifacturerCodeDump = _romDump[0x13F..0x143];
                string manifacturer = Encoding.ASCII.GetString(manifacturerCodeDump);

                _logger.Info($"Title: {title}, Manifacturer: {manifacturer}");

            }
            else // old metadata informations format
            {
                string title = Encoding.ASCII.GetString(romMetadata);
                _logger.Info($"Title: {title}");
            }

            //0143 — CGB flag
            _logger.Info($"CBG flag set to: ${romMetadata[romMetadata.Length - 1].ToString("X2")}");

            //0144–0145 — New licensee code or 014B — Old licensee code
            byte licenseCode = _romDump[0x14B];
            if (licenseCode == 0x33)
            {
                byte[] newlicenseCode = _romDump[0x144..0x146];
                string license = Encoding.ASCII.GetString(newlicenseCode);
                _logger.Info($"Licensee: {_newLicenseeCodes[license]}");

            }
            else
            {
                string license = licenseCode.ToString("X2");
                _logger.Info($"Licensee: {_oldLicenseeCodes[license]}");
            }


            //0146 — SGB flag
            string sbgFlag = _romDump[0x146] == 0x00 ? "not compatible" : _romDump[0x146] == 0x03 ? "compatible" : "invalid code";

            _logger.Info($"SBG flag set to: {sbgFlag}");

            string destination = _romDump[0x014A] == 0x00 ? "Japan (and possibly overseas)" : _romDump[0x014A] == 0x01 ? "Overseas only" : "Invalid Destination Code";
            _logger.Info($"Destination: {destination}");

            byte gameVersion = _romDump[0x014C];
            _logger.Info($"Game Version: {gameVersion}");

        }
        private void HeaderChecksum()
        {
            byte checksum = 0;

            for (ushort address = 0x0134; address < 0x014D; address++)
            {
                checksum = (byte)(checksum - _romDump![address] - 1);
            }

            if ((byte)(checksum & 0xFF) != _romDump![0x014D])
            {
                _logger.Warn("Checksum failed");
            }
            else
            {
                _logger.Info("Checksum OK");
            }
        }

        private void ReadCartridgeMBC()
        {
            //TODO ReadCartridgeHardware
        }

    }
}
