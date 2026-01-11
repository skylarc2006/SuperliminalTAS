using MemUtil;
using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Screen = UnityEngine.Screen;

namespace SuperliminalTAS
{
	public class DemoRecorder : MonoBehaviour
	{
		private int frame;
		private bool recording, playingBack = false;

		private Dictionary<string, List<bool>> button;
		private Dictionary<string, List<bool>> buttonDown;
		private Dictionary<string, List<bool>> buttonUp;
		private Dictionary<string, List<float>> axis;
		private Text statusText;
        private int baseFontSize;
        private bool lastShowInterface = true;
        private bool showInterface = true;
		private bool showPlayerVariables = true;
		private bool showKeybinds = true;
		private float prev_x, prev_y, prev_z = 0.0f;
		private float speedhackPercentage = 30.0f;
		private bool speedhackEnabled = false;
		const int NORMAL_FRAME_RATE = 50;
		const float SPEEDHACK_MINIMUM_PERCENTAGE = 5.0f;
		const float SPEEDHACK_MAXIMUM_PERCENTAGE = 1000.0f;
		private bool startFromCheckpoint = false;

        private readonly StandaloneFileBrowserWindows fileBrowser = new();
        private readonly ExtensionFilter[] extensionList = new[] {
			new SFB.ExtensionFilter("Superliminal TAS Recording (*.slt)", "slt"),
			new SFB.ExtensionFilter("All Files", "*")
		};
		private readonly string demoDirectory = AppDomain.CurrentDomain.BaseDirectory + "\\demos";

        private void Awake()
		{
			Application.targetFrameRate = NORMAL_FRAME_RATE;
            if (!Directory.Exists(demoDirectory))
			{
				Directory.CreateDirectory(demoDirectory);
			}
			ResetLists();
		}

        private void ReloadCheckpoint()
        {
            GameManager.GM.TriggerScenePreUnload();
            GameManager.GM.GetComponent<SaveAndCheckpointManager>().ResetToLastCheckpoint();
        }

        private void RestartMap()
        {
            GameManager.GM.TriggerScenePreUnload();
            GameManager.GM.GetComponent<SaveAndCheckpointManager>().RestartLevel();
        }

		private void ResetLevelState()
		{
			if (startFromCheckpoint)
			{
				ReloadCheckpoint();
			}
			else
			{
				RestartMap();
            }
        }

        private void ToggleSpeedhack()
		{
            if (speedhackEnabled)
            {
                Application.targetFrameRate = NORMAL_FRAME_RATE;
                speedhackEnabled = false;
            }
            else
            {
                Application.targetFrameRate = (int)(NORMAL_FRAME_RATE * speedhackPercentage / 100.0f);
                speedhackEnabled = true;
            }
        }

		private void ChangeSpeedhackPercentage()
		{
            float percentageChange = Input.mouseScrollDelta.y * 5.0f;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                percentageChange *= 5.0f;
            }

            speedhackPercentage += percentageChange;
            if (speedhackPercentage < SPEEDHACK_MINIMUM_PERCENTAGE)
                speedhackPercentage = SPEEDHACK_MINIMUM_PERCENTAGE;
            if (speedhackPercentage > SPEEDHACK_MAXIMUM_PERCENTAGE)
                speedhackPercentage = SPEEDHACK_MAXIMUM_PERCENTAGE;
            if (speedhackEnabled)
            {
                Application.targetFrameRate = (int)(NORMAL_FRAME_RATE * speedhackPercentage / 100.0f);
            }
        }

        private void LateUpdate()
		{
            HandleInput();
			if (recording)
			{
				RecordInputs();
				frame++;
			}
			else if (playingBack)
			{
				frame++;
				if (frame > button["Jump"].Count)
				{
					StopPlayback();
				}
			}

            if (statusText == null && GameObject.Find("UI_PAUSE_MENU") != null)
			{
				GenerateStatusText();
			}

            if (statusText != null)
            {
                statusText.text = "SuperliminalTAS v0.2.0";

                // Detect transition
                if (showInterface != lastShowInterface)
				{
                    if (showInterface)
                    {
                        statusText.fontSize = baseFontSize;
                    }
                    else
                    {
                        statusText.fontSize = baseFontSize / 2;
                    }

                    lastShowInterface = showInterface;
                }

                if (showInterface)
                {
                    if (playingBack)
                        statusText.text += "\n\nplayback: " + (frame - 1) + " / " + button["Jump"].Count;
                    else if (recording)
                        statusText.text += "\n\nrecording: " + frame + " / ?";
                    else
                        statusText.text += "\n\nstopped: 0 / " + button["Jump"].Count;

					statusText.text += $"\n\nSpeedhack: {(speedhackEnabled ? "Enabled" : "Disabled")} ({speedhackPercentage}%)";

                    if (showPlayerVariables && GameManager.GM.player != null)
                    {
                        var playerPos = GameManager.GM.player.transform.position;
                        Vector3f playerVel = new(
                            playerPos.x - prev_x,
                            playerPos.y - prev_y,
                            playerPos.z - prev_z
                        );


                        statusText.text +=
                            $"\n\nx: {playerPos.x:0.0000}\n" +
                            $"y: {playerPos.y:0.0000}\n" +
                            $"z: {playerPos.z:0.0000}";

						statusText.text +=
							$"\n\nvel x: {playerVel.X:0.0000}\n" +
							$"vel z: {playerVel.Z:0.0000}\n";

						float horizontalVelocity = (float)Math.Sqrt(playerVel.X * playerVel.X + playerVel.Z * playerVel.Z);

						statusText.text +=
							$"Horizontal Velocity: {horizontalVelocity:0.0000}\n" +
							$"Vertical Velocity: {playerVel.Y:0.0000}";

                        prev_x = playerPos.x;
                        prev_y = playerPos.y;
                        prev_z = playerPos.z;
                    }

                    if (showKeybinds)
                    {
                        statusText.text += "\n\n" +
                            "F1 - Toggle Show Interface\n" +
                            "F2 - Toggle Show Player Variables\n" +
                            "F3 - Toggle Show Keybinds\n\n" +

							"F4 - Toggle Speedhack\n" +
							"Mouse Wheel Up/Down - Adjust Percentage\n\n" +
							
							/*
							"F5 - Save State\n" +
							"F6 - Load State\n\n" +
							*/

							"F7 - Toggle Start from Restart Level / Last Checkpoint\n" +
							"Current: " + (startFromCheckpoint ? "Last Checkpoint" : "Restart Level") + "\n\n" +

                            "F8 - Play\n" +
                            "F9 - Stop\n" +
                            "F10 - Record\n" +
                            "F11 - Open\n" +
                            "F12 - Save";
                    }
                }
            }
        }


		private void HandleInput()
		{
            if (recording)
			{
				if (Input.GetKeyDown(KeyCode.F9))
				{
					StopRecording();
				}
			}
			else if (playingBack)
			{
				if (Input.GetKeyDown(KeyCode.F9))
				{
					StopPlayback();
				}
			}
			else
			{
				if (Input.GetKeyDown(KeyCode.F8))
				{
					StartPlayback();
				}
				else if (Input.GetKeyDown(KeyCode.F10))
				{
					StartRecording();
				}
			}

            if (Input.GetKeyDown(KeyCode.F1))
            {
                showInterface = !showInterface;
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                showPlayerVariables = !showPlayerVariables;
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                showKeybinds = !showKeybinds;
            }
            if (Input.GetKeyDown(KeyCode.F4))
			{
				ToggleSpeedhack();
            }
			if (Input.mouseScrollDelta.y != 0)
			{
				ChangeSpeedhackPercentage();
            }

			/*
			if (Input.GetKeyDown(KeyCode.F5))
			{
				SaveState();
            }
			if (Input.GetKeyDown(KeyCode.F6))
			{
				LoadState();
            }
			*/

            if (Input.GetKeyDown(KeyCode.F7))
			{
				startFromCheckpoint = !startFromCheckpoint;
            }

			if (Input.GetKeyDown(KeyCode.F11))
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                OpenDemo();
                UnityEngine.Cursor.visible = false;
            }
            if (Input.GetKeyDown(KeyCode.F12))
			{
				UnityEngine.Cursor.lockState = CursorLockMode.None;
				UnityEngine.Cursor.visible = true;
				SaveDemo();
				UnityEngine.Cursor.visible = false;
			}
        }

		private void OpenDemo()
		{
			var selectedFile = fileBrowser.OpenFilePanel("Open", demoDirectory, extensionList, false);
			if (selectedFile.FirstOrDefault() != null)
			{
				var stream = File.OpenRead(selectedFile.FirstOrDefault()?.Name);
				if (stream != null)
				{
					ReadFromFileStream(stream);
				}
			}
		}

		private void SaveDemo()
		{
			if (button["Jump"].Count == 0)
				return;

			var selectedFile = fileBrowser.SaveFilePanel("Save Recording as", demoDirectory, $"SuperliminalTAS-{System.DateTime.Now:yyyy-MM-dd-HH-mm-ss}.slt", extensionList);
			if (selectedFile != null)
			{
				if (!selectedFile.Name.EndsWith(".slt"))
					selectedFile.Name += ".slt";
				File.WriteAllBytes(selectedFile.Name, SerializeToByteArray());
			}
		}

		private void StartRecording()
		{
			ResetLists();
			recording = true;
            TASInput.StopPlayback();
			ResetLevelState();
            frame = 0;
			GameManager.GM.GetComponent<PlayerSettingsManager>()?.SetMouseSensitivity(2.0f);
		}

		private void ResetLists()
		{
			button = new()
			{
				["Jump"] = new(),
				["Grab"] = new(),
				["Rotate"] = new()
			};

			buttonUp = new()
			{
				["Jump"] = new(),
				["Grab"] = new(),
				["Rotate"] = new()
			};

			buttonDown = new()
			{
				["Jump"] = new(),
				["Grab"] = new(),
				["Rotate"] = new()
			};

			axis = new()
			{
				["Move Horizontal"] = new(),
				["Move Vertical"] = new(),
				["Look Horizontal"] = new(),
				["Look Vertical"] = new()
			};
		}

		private void StopRecording()
		{
			recording = false;
			frame = 0;
		}

		private void RecordInputs()
		{
			button["Jump"].Add(GameManager.GM.playerInput.GetButton("Jump"));
			button["Grab"].Add(GameManager.GM.playerInput.GetButton("Grab"));
			button["Rotate"].Add(GameManager.GM.playerInput.GetButton("Rotate"));

			buttonUp["Jump"].Add(GameManager.GM.playerInput.GetButtonUp("Jump"));
			buttonUp["Grab"].Add(GameManager.GM.playerInput.GetButtonUp("Grab"));
			buttonUp["Rotate"].Add(GameManager.GM.playerInput.GetButtonUp("Rotate"));

			buttonDown["Jump"].Add(GameManager.GM.playerInput.GetButtonDown("Jump"));
			buttonDown["Grab"].Add(GameManager.GM.playerInput.GetButtonDown("Grab"));
			buttonDown["Rotate"].Add(GameManager.GM.playerInput.GetButtonDown("Rotate"));

			axis["Move Horizontal"].Add(GameManager.GM.playerInput.GetAxis("Move Horizontal"));
			axis["Move Vertical"].Add(GameManager.GM.playerInput.GetAxis("Move Vertical"));
			axis["Look Horizontal"].Add(GameManager.GM.playerInput.GetAxis("Look Horizontal"));
			axis["Look Vertical"].Add(GameManager.GM.playerInput.GetAxis("Look Vertical"));
		}

		private void StartPlayback()
		{
			if (button["Jump"].Count < 1)
				return;
			recording = false;
			playingBack = true;
			TASInput.StartPlayback(this);
            ResetLevelState();
            frame = 1;
			GameManager.GM.GetComponent<PlayerSettingsManager>()?.SetMouseSensitivity(2.0f);
		}

		private void StopPlayback()
		{
			recording = false;
			playingBack = false;
			TASInput.StopPlayback();
			frame = 0;
		}

		internal bool GetRecordedButton(string actionName)
		{
			return button[actionName][frame - 1];
		}

		internal bool GetRecordedButtonDown(string actionName)
		{
			return buttonDown[actionName][frame - 1];
		}

		internal bool GetRecordedButtonUp(string actionName)
		{
			return buttonUp[actionName][frame - 1];
		}

		internal float GetRecordedAxis(string actionName)
		{
			return axis[actionName][frame - 1];
		}

		private void GenerateStatusText()
		{
			GameObject gameObject = new("TASMod_UI");
			gameObject.transform.parent = GameObject.Find("UI_PAUSE_MENU").transform.Find("Canvas");
			gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

			statusText = gameObject.AddComponent<Text>();
            statusText.fontSize = (int)(24 * Screen.height / 1080.0);
            baseFontSize = statusText.fontSize;
            if (!showInterface)
            {
                statusText.fontSize /= 2;
            }

            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
				if (font.name == "NotoSans-CondensedSemiBold")
					statusText.font = font;

			var rect = statusText.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(Screen.width / 3, Screen.height);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(25f, -25f);
		}

		private byte[] SerializeToByteArray()
		{
			if (button["Jump"].Count < 1)
			{
				return null;
			}

			string magic = "SUPERLIMINALTAS2";
			byte[] magicBytes = Encoding.ASCII.GetBytes(magic);
			byte[] lengthBytes = BitConverter.GetBytes(button["Jump"].Count);
			Dictionary<string, byte[]> axisBytes = new()
			{
				["Move Horizontal"] = FloatListToByteArray(axis["Move Horizontal"]),
				["Move Vertical"] = FloatListToByteArray(axis["Move Vertical"]),
				["Look Horizontal"] = FloatListToByteArray(axis["Look Horizontal"]),
				["Look Vertical"] = FloatListToByteArray(axis["Look Vertical"])
			};
			Dictionary<string, byte[]> buttonBytes = new()
			{
				["Jump"] = BoolListToByteArray(button["Jump"]),
				["Grab"] = BoolListToByteArray(button["Grab"]),
				["Rotate"] = BoolListToByteArray(button["Rotate"])
			};
			Dictionary<string, byte[]> buttonDownBytes = new()
			{
				["Jump"] = BoolListToByteArray(buttonDown["Jump"]),
				["Grab"] = BoolListToByteArray(buttonDown["Grab"]),
				["Rotate"] = BoolListToByteArray(buttonDown["Rotate"])
			};
			Dictionary<string, byte[]> buttonUpBytes = new()
			{
				["Jump"] = BoolListToByteArray(buttonUp["Jump"]),
				["Grab"] = BoolListToByteArray(buttonUp["Grab"]),
				["Rotate"] = BoolListToByteArray(buttonUp["Rotate"])
			};

			byte[] result;
			using (MemoryStream memoryStream = new())
			{
                // .slt File Structure:
                // 16 bytes: Magic "SUPERLIMINALTAS2"
                // 4 bytes (int32): Length of the replay in frames

                // Then, for each frame:
                // 4 bytes (float): Move Horizontal
                // 4 bytes (float): Move Vertical
                // 4 bytes (float): Look Horizontal
                // 4 bytes (float): Look Vertical
                // 1 byte (bool): Jump
                // 1 byte (bool): Grab
                // 1 byte (bool): Rotate
                // 1 byte (bool): Jump Button Pressed
                // 1 byte (bool): Grab Button Pressed
                // 1 byte (bool): Rotate Button Pressed
                // 1 byte (bool): Jump Button Released
                // 1 byte (bool): Grab Button Released
                // 1 byte (bool): Rotate Button Released


                memoryStream.Write(magicBytes, 0, magicBytes.Length);
				memoryStream.Write(lengthBytes, 0, lengthBytes.Length);

				for (int i = 0; i < button["Jump"].Count; i++)
				{
                    memoryStream.Write(axisBytes["Move Horizontal"], i * 4, 4);
                    memoryStream.Write(axisBytes["Move Vertical"], i * 4, 4);
                    memoryStream.Write(axisBytes["Look Horizontal"], i * 4, 4);
                    memoryStream.Write(axisBytes["Look Vertical"], i * 4, 4);

                    memoryStream.Write(buttonBytes["Jump"], i, 1);
                    memoryStream.Write(buttonBytes["Grab"], i, 1);
                    memoryStream.Write(buttonBytes["Rotate"], i, 1);

                    memoryStream.Write(buttonDownBytes["Jump"], i, 1);
                    memoryStream.Write(buttonDownBytes["Grab"], i, 1);
                    memoryStream.Write(buttonDownBytes["Rotate"], i, 1);

                    memoryStream.Write(buttonUpBytes["Jump"], i, 1);
                    memoryStream.Write(buttonUpBytes["Grab"], i, 1);
                    memoryStream.Write(buttonUpBytes["Rotate"], i, 1);
                }

				result = memoryStream.ToArray();
			}

			return result;
		}

        private bool ReadFromFileStream(FileStream stream)
        {
            // magic
            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, buffer.Length);
            var magic = Encoding.ASCII.GetString(buffer);

            if (magic != "SUPERLIMINALTAS2")
                return false;

            // replay length in frames
            buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            int length = BitConverter.ToInt32(buffer, 0);

            // allocate lists
            axis = new()
            {
                ["Move Horizontal"] = new List<float>(length),
                ["Move Vertical"] = new List<float>(length),
                ["Look Horizontal"] = new List<float>(length),
                ["Look Vertical"] = new List<float>(length),
            };

            button = new()
            {
                ["Jump"] = new List<bool>(length),
                ["Grab"] = new List<bool>(length),
                ["Rotate"] = new List<bool>(length),
            };

            buttonDown = new()
            {
                ["Jump"] = new List<bool>(length),
                ["Grab"] = new List<bool>(length),
                ["Rotate"] = new List<bool>(length),
            };

            buttonUp = new()
            {
                ["Jump"] = new List<bool>(length),
                ["Grab"] = new List<bool>(length),
                ["Rotate"] = new List<bool>(length),
            };

            // read data per frame
            byte[] floatBuf = new byte[4];
            byte[] boolBuf = new byte[1];

            for (int i = 0; i < length; i++)
            {
                // move and look axes
                stream.Read(floatBuf, 0, 4);
                axis["Move Horizontal"].Add(BitConverter.ToSingle(floatBuf, 0));

                stream.Read(floatBuf, 0, 4);
                axis["Move Vertical"].Add(BitConverter.ToSingle(floatBuf, 0));

                stream.Read(floatBuf, 0, 4);
                axis["Look Horizontal"].Add(BitConverter.ToSingle(floatBuf, 0));

                stream.Read(floatBuf, 0, 4);
                axis["Look Vertical"].Add(BitConverter.ToSingle(floatBuf, 0));

                // buttons
                stream.Read(boolBuf, 0, 1);
                button["Jump"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                button["Grab"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                button["Rotate"].Add(boolBuf[0] != 0);

                // button down
                stream.Read(boolBuf, 0, 1);
                buttonDown["Jump"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                buttonDown["Grab"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                buttonDown["Rotate"].Add(boolBuf[0] != 0);

                // button up
                stream.Read(boolBuf, 0, 1);
                buttonUp["Jump"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                buttonUp["Grab"].Add(boolBuf[0] != 0);

                stream.Read(boolBuf, 0, 1);
                buttonUp["Rotate"].Add(boolBuf[0] != 0);
            }

            return true;
        }

        private List<float> DeserializeFloatList(byte[] buffer)
		{
			List<float> result = new();

			for (int i = 0; i < buffer.Length / 4; i++)
			{
				result.Add(BitConverter.ToSingle(buffer, i * 4));
			}

			return result;
		}

		private List<bool> DeserializeBoolList(byte[] buffer)
		{
			List<bool> result = new();

			for (int i = 0; i < buffer.Length; i++)
			{
				result.Add(BitConverter.ToBoolean(buffer, i));
			}

			return result;
		}

		private byte[] FloatListToByteArray(List<float> list)
		{
			byte[] result;
			using (MemoryStream memoryStream = new())
			{
				foreach (float value in list)
				{
					byte[] buffer = BitConverter.GetBytes(value);
					memoryStream.Write(buffer, 0, buffer.Length);
				}
				result = memoryStream.ToArray();
			}
			return result;
		}

		private byte[] BoolListToByteArray(List<bool> list)
		{
			byte[] result;
			using (MemoryStream memoryStream = new())
			{
				foreach (bool value in list)
				{
					byte[] buffer = BitConverter.GetBytes(value);
					memoryStream.Write(buffer, 0, buffer.Length);
				}
				result = memoryStream.ToArray();
			}
			return result;
		}
	}
}