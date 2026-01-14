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
        private bool firstFrameOfRecording = false;
		private bool startFromCheckpoint = false;
        private string currentLoadedFile = "";
        private bool levelFadeEnabled = true;
        private GameObject flashlight;
        private GameObject player;

        private readonly StandaloneFileBrowserWindows fileBrowser = new();
		private readonly ExtensionFilter[] extensionList = new[] {
			new SFB.ExtensionFilter("Superliminal TAS (*.slt, *.csv)", "slt", "csv"),
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

        private void RefreshDemo()
        {
            if (currentLoadedFile.EndsWith(".csv"))
            {
                LoadFromCsv(currentLoadedFile);
            }
            else
            {
                using var stream = File.OpenRead(currentLoadedFile);
                ReadFromFileStream(stream);
            }
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
            if (GameManager.GM.player != null)
            {
                player = GameManager.GM.player;
                if (player.transform.Find("Flashlight") == null)
                {
                    flashlight = new GameObject("Flashlight");
                    flashlight.SetActive(false);
                    this.flashlight.transform.parent = player.transform;
                    this.flashlight.transform.localPosition = new Vector3(0f, player.GetComponentInChildren<Camera>().transform.localPosition.y, 0f);
                    Light light = this.flashlight.AddComponent<Light>();
                    light.range = 10000f;
                    light.intensity = 0.5f;
                }
                else
                {
                    flashlight = GameManager.GM.player.transform.Find("Flashlight").gameObject;
                }
                // Remove level fade if disabled, original feature made by yyna
                if (!levelFadeEnabled)
                {
                    GameManager.GM.player.transform.Find("GUI Camera").GetComponent<FadeCameraToBlack>().enabled = false;
                    GameManager.GM.player.transform.Find("GUI Camera/Canvas/Fade").gameObject.SetActive(false);

                    if ((UnityEngine.Object)GameObject.Find("UI_PAUSE_MENU/Canvas/SavingIcon") != (UnityEngine.Object)null)
                    {
                        UnityEngine.Object.Destroy((UnityEngine.Object)GameObject.Find("UI_PAUSE_MENU/Canvas/SavingIcon"));
                    }
                    if ((UnityEngine.Object)GameObject.Find("Global/UI_PAUSE_MENU/Canvas/SavingIcon") != (UnityEngine.Object)null)
                    {
                        UnityEngine.Object.Destroy((UnityEngine.Object)GameObject.Find("Global/UI_PAUSE_MENU/Canvas/SavingIcon"));
                    }
                }
            }

            HandleInput();
            if (recording && firstFrameOfRecording)
            {
                firstFrameOfRecording = false;
            }
            else if (recording)
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
                    statusText.text += $"\n\nCurrent File: {(currentLoadedFile == "" ? "None" : Path.GetFileName(currentLoadedFile))}";
                    if (playingBack)
                        statusText.text += "\nplayback: " + (frame - 1) + " / " + button["Jump"].Count;
                    else if (recording)
                        statusText.text += "\nrecording: " + frame + " / ?";
                    else
                        statusText.text += "\nstopped: 0 / " + button["Jump"].Count;

					statusText.text += $"\n\nSpeedhack: {(speedhackEnabled ? "Enabled" : "Disabled")} ({speedhackPercentage}%)";

                    if (showPlayerVariables && GameManager.GM.player != null)
                    {
                        var playerPos = GameManager.GM.player.transform.position;

                        statusText.text += $"\n\nPosition: {playerPos.x:0.00000}, {playerPos.y:0.00000}, {playerPos.z:0.00000}\n";

                        var camera = GameManager.GM.player.GetComponentInChildren<Camera>();
                        var rotationX = camera.transform.rotation.eulerAngles.x;
                        var rotationY = camera.transform.rotation.eulerAngles.y;

                        statusText.text += $"Rotation: {rotationY:0.00000}, {rotationX:0.00000}\n";

                        CharacterMotor playerMotor = GameManager.GM.player.GetComponent<CharacterMotor>();
                        float scale = playerMotor.transform.localScale.x;
                        statusText.text += $"Scale: {scale:0.00000}x";

                        ResizeScript resizeScript = camera.GetComponent<ResizeScript>();

                        Vector3f playerVel = new(
                            playerPos.x - prev_x,
                            playerPos.y - prev_y,
                            playerPos.z - prev_z
                        );
                        float horizontalVelocity = (float)Math.Sqrt(playerVel.X * playerVel.X + playerVel.Z * playerVel.Z);

                        statusText.text += $"\n\nVelocity (X, Z): {playerVel.X:0.00000}, {playerVel.Z:0.00000}\n";

                        statusText.text +=
                            $"Horizontal Velocity: {horizontalVelocity:0.00000}\n" +
                            $"Vertical Velocity: {playerVel.Y:0.00000}";

                        if (resizeScript.isGrabbing && resizeScript.GetGrabbedObject() != null)
                        {
                            GameObject grabbedObject = resizeScript.GetGrabbedObject();
                            string output = string.Concat(new object[]{
                                grabbedObject.name+"\n",
                                "Position: "+grabbedObject.transform.position.x.ToString("0.00000")+", "+grabbedObject.transform.position.y.ToString("0.00000")+", "+grabbedObject.transform.position.z.ToString("0.00000")+"\n",
                                "Scale: "+grabbedObject.transform.localScale.x.ToString("0.00000")+"x"
                            });
                            if (grabbedObject.GetComponent<Collider>() != null)
                            {
                                Collider playerCollider = player.GetComponent<Collider>();
                                Collider objectCollider = grabbedObject.GetComponent<Collider>();
                                if (
                                    Physics.ComputePenetration(playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
                                        objectCollider, objectCollider.transform.position, objectCollider.transform.rotation,
                                        out Vector3 direction, out float distance))
                                {
                                    Vector3 warpPrediction = player.transform.position + direction * distance;
                                    if (distance > 5)
                                    {
                                        output += "\nWarp Prediction: " + warpPrediction.x.ToString("0.00000") + ", " + warpPrediction.y.ToString("0.00000") + ", " + warpPrediction.z.ToString("0.00000");
                                        output += "\nWarp Distance: " + distance.ToString("0.00000");
                                    }
                                }
                            }
                            statusText.text += "\n\nGrabbed Object:\n" + output;
                        }

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

                            "F5 - Toggle Flashlight\n" +
                            "F6 - Toggle Level Fade (" + (levelFadeEnabled ? "Enabled" : "Disabled") + ")\n" +
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
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (flashlight != null)
                {
                    flashlight.gameObject.SetActive(!flashlight.gameObject.activeSelf);
                }
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                levelFadeEnabled = !levelFadeEnabled;
            }
            if (Input.mouseScrollDelta.y != 0)
			{
				ChangeSpeedhackPercentage();
            }
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

            var file = selectedFile.FirstOrDefault();
            if (file == null)
                return;

            if (file.Name.EndsWith(".csv"))
            {
                LoadFromCsv(file.Name);
            }
            else
            {
                using var stream = File.OpenRead(file.Name);
                ReadFromFileStream(stream);
            }
            currentLoadedFile = file.Name;
        }

        private void SaveDemo()
        {
            if (button["Jump"].Count == 0)
                return;

            var selectedFile = fileBrowser.SaveFilePanel(
                "Save Recording as",
                demoDirectory,
                $"SuperliminalTAS-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}",
                new[] {
            new SFB.ExtensionFilter("Superliminal TAS (*.slt)", "slt"),
            new SFB.ExtensionFilter("All Files", "*")
                }
            );

            if (selectedFile == null)
                return;
            else
            {
                if (!selectedFile.Name.EndsWith(".slt"))
                    selectedFile.Name += ".slt";

                File.WriteAllBytes(selectedFile.Name, SerializeToByteArray());
            }
            currentLoadedFile = selectedFile.Name;
            RefreshDemo();

        }

        private void StartRecording()
		{
            currentLoadedFile = "";
			ResetLists();
			recording = true;
            firstFrameOfRecording = true;
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
            if (currentLoadedFile != "")
                RefreshDemo();
            recording = false;
			playingBack = true;
			TASInput.StartPlayback(this);
            ResetLevelState();
            frame = 0;
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

        private float Accelerate(float velocity, bool backwards)
        {
            velocity += backwards ? -0.06f : 0.06f;
            return Mathf.Clamp(velocity, -1f, 1f);
        }

        private float Decelerate(float velocity)
        {
            if (Mathf.Approximately(velocity, 0f))
                return 0f;

            bool negative = velocity < 0f;
            velocity += negative ? 0.06f : -0.06f;

            if ((velocity < 0f && !negative) || (velocity > 0f && negative))
                return 0f;

            return velocity;
        }

        private bool LoadFromCsv(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return false;

            ResetLists();

            float moveH = 0f;
            float moveV = 0f;

            bool prevJump = false;
            bool prevGrab = false;
            bool prevRotate = false;

            var lookCols = lines[0].Split(',');
            float prevLookX = float.Parse(lookCols[4]);
            float prevLookY = float.Parse(lookCols[5]);

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');

                bool w = cols[0] == "1";
                bool a = cols[1] == "1";
                bool s = cols[2] == "1";
                bool d = cols[3] == "1";

                float lookX = (float.Parse(cols[4]) - prevLookX) / 2.0f;
                float lookY = (prevLookY - float.Parse(cols[5])) / 2.0f;

                prevLookX = float.Parse(cols[4]);
                prevLookY = float.Parse(cols[5]);

                bool jump = cols[6] == "1";
                bool grab = cols[7] == "1";
                bool rotate = cols[8] == "1";

                // movement reconstruction
                if (i == 1)
                {
                    moveV = 0f;
                    moveH = 0f;

                    if (w && !s)
                        moveV = 1f;
                    else if (s && !w)
                        moveV = -1f;

                    if (d && !a)
                        moveH = 1f;
                    else if (a && !d)
                        moveH = -1f;
                }
                else
                {
                    // Subsequent frames: use acceleration/deceleration
                    if (w && !s)
                        moveV = Accelerate(moveV, false);
                    else if (s && !w)
                        moveV = Accelerate(moveV, true);
                    else
                        moveV = Decelerate(moveV);

                    if (d && !a)
                        moveH = Accelerate(moveH, false);
                    else if (a && !d)
                        moveH = Accelerate(moveH, true);
                    else
                        moveH = Decelerate(moveH);
                }

                // axes
                axis["Move Horizontal"].Add(moveH);
                axis["Move Vertical"].Add(moveV);
                axis["Look Horizontal"].Add(lookX);
                axis["Look Vertical"].Add(lookY);

                // buttons
                button["Jump"].Add(jump);
                button["Grab"].Add(grab);
                button["Rotate"].Add(rotate);

                buttonDown["Jump"].Add(jump && !prevJump);
                buttonDown["Grab"].Add(grab && !prevGrab);
                buttonDown["Rotate"].Add(rotate && !prevRotate);

                buttonUp["Jump"].Add(!jump && prevJump);
                buttonUp["Grab"].Add(!grab && prevGrab);
                buttonUp["Rotate"].Add(!rotate && prevRotate);

                prevJump = jump;
                prevGrab = grab;
                prevRotate = rotate;
            }

            return true;
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

        private static int BoolToInt(bool v) => v ? 1 : 0;

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