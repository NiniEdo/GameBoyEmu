﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SDL2;

namespace GameBoyEmu.ScreenNameSpace
{
    internal class Screen
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();

        private IntPtr _window;
        private IntPtr _renderer;
        private const int SCREEN_MULTIPLIER = 4;
        private const int SCREEN_WIDTH_PIXELS = 160;
        private const int SCREEN_HEIGHT_PIXELS = 144;
        private const int SCREEN_WIDTH = SCREEN_WIDTH_PIXELS * SCREEN_MULTIPLIER;
        private const int SCREEN_HEIGHT = SCREEN_HEIGHT_PIXELS * SCREEN_MULTIPLIER;

        public Screen()
        { }

        public void InitScreen()
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                _logger.Error($"SDL Initialization failed: {SDL.SDL_GetError()}");
                return;
            }

            _window = SDL.SDL_CreateWindow(
                 "GameBoyEmu By Edoardo Nini",
                 SDL.SDL_WINDOWPOS_CENTERED,
                 SDL.SDL_WINDOWPOS_CENTERED,
                 SCREEN_WIDTH,
                 SCREEN_HEIGHT,
                 SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
            );

            if (_window == IntPtr.Zero)
            {
                _logger.Error($"Creation of SDL window failed: {SDL.SDL_GetError()}");
                return;
            }

            _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            if (_renderer == IntPtr.Zero)
            {
                _logger.Error($"Creation of SDL render failed: {SDL.SDL_GetError()}");
                return;
            }

            DrawTiles();
        }

        public void RenderScreen()
        {

        }

        public static void ListenForEvents(ref bool isRunning)
        {
            SDL.SDL_Event e;

            while (SDL.SDL_PollEvent(out e) != 0)
            {
                if (e.type == SDL.SDL_EventType.SDL_QUIT)
                {
                    isRunning = false;
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
