using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.gameboy;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.Utils;
using NLog;
using SDL2;
using static System.Formats.Asn1.AsnWriter;
using static SDL2.SDL;

namespace GameBoyEmu.ScreenNameSpace
{
    internal class Screen
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private static Screen? _instance;

        private nint _window;
        private nint _renderer;
            
        private const int SCREEN_MULTIPLIER = 4;
        private const int SCREEN_WIDTH_PIXELS = 160;
        private const int SCREEN_HEIGHT_PIXELS = 144;
        private const int SCREEN_WIDTH = SCREEN_WIDTH_PIXELS * SCREEN_MULTIPLIER;
        private const int SCREEN_HEIGHT = SCREEN_HEIGHT_PIXELS * SCREEN_MULTIPLIER;

        private Screen()
        { }

        public static Screen GetInstance()
        {
            if (_instance == null)
            {
                _instance = new Screen();
            }
            return _instance;
        }

        public void InitScreen()
        {
            _ = SDL_Init(SDL_INIT_VIDEO);
            _window = SDL_CreateWindow("SpecBoy", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, SCREEN_WIDTH, SCREEN_HEIGHT, SDL_WindowFlags.SDL_WINDOW_SHOWN);
            _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        }

        private static readonly byte[][] GreenPalette = {
            new byte[] { 155, 188, 15 },
            new byte[] { 139, 172, 15 },
            new byte[] { 48, 98, 48 },
            new byte[] { 15, 56, 15 }
        };

        public void RenderPixel(byte x, byte y, byte pixel)
        {
            byte[] color = GreenPalette[pixel];
            SDL.SDL_SetRenderDrawColor(_renderer, color[0], color[1], color[2], 255);

            SDL.SDL_Rect pixelRect = new SDL.SDL_Rect
            {
                x = x * SCREEN_MULTIPLIER,
                y = y * SCREEN_MULTIPLIER,
                w = SCREEN_MULTIPLIER,
                h = SCREEN_MULTIPLIER
            };

            SDL.SDL_RenderFillRect(_renderer, ref pixelRect);
        }

        public void PresentScreen()
        {
            SDL.SDL_RenderPresent(_renderer);
        }

        public static void ListenForEvents()
        {
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT:
                        GameBoy.IsRunning = false;
                        break;
                }
            }
        }

        public void CloseScreen()
        {
            SDL.SDL_DestroyRenderer(_renderer);
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
        }

        private void DrawTiles()
        {
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(_renderer);

            SDL.SDL_SetRenderDrawColor(_renderer, 0, 255, 0, 255);
            List<SDL.SDL_Rect> rects = new List<SDL.SDL_Rect>();

            for (int i = 0; i < SCREEN_WIDTH_PIXELS; i++)
            {
                for (int j = 0; j < SCREEN_HEIGHT_PIXELS; j++)
                {
                    SDL.SDL_Rect rect = new SDL.SDL_Rect
                    {
                        x = i * 8 * SCREEN_MULTIPLIER,
                        y = j * 8 * SCREEN_MULTIPLIER,
                        w = 8 * SCREEN_MULTIPLIER,
                        h = 8 * SCREEN_MULTIPLIER
                    };
                    rects.Add(rect);
                }
            }

            SDL.SDL_RenderDrawRects(_renderer, rects.ToArray(), rects.Count);
            SDL.SDL_RenderPresent(_renderer);
        }
    }
}
