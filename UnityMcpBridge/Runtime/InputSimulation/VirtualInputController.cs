using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// Manages virtual input devices and dispatches low-level input events.
    /// </summary>
    public sealed class VirtualInputController : IDisposable
    {
        private static class DeviceNames
        {
            public const string Mouse = "VirtualMouse";
            public const string Keyboard = "VirtualKeyboard";
            public const string Touchscreen = "VirtualTouchscreen";
        }
        private Mouse _virtualMouse;
        private Keyboard _virtualKeyboard;
        private Touchscreen _virtualTouchscreen;
        private bool _isInitialized;
        private MouseState _currentMouseState;

        public bool IsInitialized => _isInitialized;
        public Mouse VirtualMouse => _virtualMouse;
        public Keyboard VirtualKeyboard => _virtualKeyboard;
        public Touchscreen VirtualTouchscreen => _virtualTouchscreen;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _virtualMouse = TryGetOrCreateDevice<Mouse>(DeviceNames.Mouse);
            _virtualKeyboard = TryGetOrCreateDevice<Keyboard>(DeviceNames.Keyboard);
            _virtualTouchscreen = TryGetOrCreateDevice<Touchscreen>(DeviceNames.Touchscreen);

            _isInitialized = _virtualMouse != null && _virtualKeyboard != null;
            if (_isInitialized)
            {
                _currentMouseState = new MouseState();
            }
        }

        public void Dispose()
        {
            TryRemoveDevice(_virtualMouse);
            TryRemoveDevice(_virtualKeyboard);
            TryRemoveDevice(_virtualTouchscreen);

            _virtualMouse = null;
            _virtualKeyboard = null;
            _virtualTouchscreen = null;
            _isInitialized = false;
        }

        public void SetMousePosition(Vector2 screenPosition)
        {
            _currentMouseState.position = screenPosition;
            UpdateMouseState(_currentMouseState);
        }

        public void LeftButtonPress()
        {
            var bit = 1u << (int)MouseButton.Left;
            _currentMouseState.buttons |= (ushort)bit;
            UpdateMouseState(_currentMouseState);
        }

        public void LeftButtonRelease()
        {
            var bit = 1u << (int)MouseButton.Left;
            _currentMouseState.buttons &= (ushort)~bit;
            UpdateMouseState(_currentMouseState);
        }

        public void Scroll(Vector2 scrollDelta)
        {
            var stateWithScroll = _currentMouseState;
            stateWithScroll.scroll = scrollDelta;
            UpdateMouseState(stateWithScroll);
        }

        private void UpdateMouseState(MouseState state)
        {
            if (_virtualMouse == null || !_virtualMouse.added) return;
            InputSystem.QueueStateEvent(_virtualMouse, state);
            InputSystem.Update();
        }

        private static T TryGetOrCreateDevice<T>(string name) where T : InputDevice
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is T typed && string.Equals(device.name, name, StringComparison.Ordinal))
                {
                    return typed;
                }
            }
            try
            {
                return InputSystem.AddDevice<T>(name);
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        private static void TryRemoveDevice(InputDevice device)
        {
            if (device == null)
            {
                return;
            }
            try
            {
                if (string.Equals(device.name, DeviceNames.Mouse, StringComparison.Ordinal) ||
                    string.Equals(device.name, DeviceNames.Keyboard, StringComparison.Ordinal) ||
                    string.Equals(device.name, DeviceNames.Touchscreen, StringComparison.Ordinal))
                {
                    InputSystem.RemoveDevice(device);
                }
            }
            catch (Exception ex)
            {
            }

            return;
        }
    }
}