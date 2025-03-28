﻿using System;
using System.Collections.Generic;
using System.Drawing;
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
            if (SDL_Init(SDL_INIT_VIDEO) < 0)
            {
                _logger.Error($"SDL initialization failed: {SDL_GetError()}");
                return;
            }

            _window = SDL_CreateWindow("GameBoyEmu By Edoardo Nini", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
                                      SCREEN_WIDTH, SCREEN_HEIGHT, SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (_window == IntPtr.Zero)
            {
                _logger.Error($"Window creation failed: {SDL_GetError()}");
                return;
            }

            _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (_renderer == IntPtr.Zero)
            {
                _logger.Error($"Renderer creation failed: {SDL_GetError()}");
                return;
            }
        }

        public void RenderScanline(Color[] scanline, int y)
        {
            if (_renderer == IntPtr.Zero)
                return;

            for (int x = 0; x < scanline.Length; x++)
            {
                SDL.SDL_SetRenderDrawColor(_renderer, scanline[x].R, scanline[x].G, scanline[x].B, 255);

                SDL.SDL_Rect pixelRect = new SDL.SDL_Rect
                {
                    x = x * SCREEN_MULTIPLIER,
                    y = y * SCREEN_MULTIPLIER,
                    w = SCREEN_MULTIPLIER,
                    h = SCREEN_MULTIPLIER
                };

                SDL.SDL_RenderFillRect(_renderer, ref pixelRect);
            }
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
