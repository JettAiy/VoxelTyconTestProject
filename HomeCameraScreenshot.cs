using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using UnityEngine;
using UnityEngine.UI;

using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Game.UI.ModernUI;
using VoxelTycoon.Serialization;
using VoxelTycoon.UI;
using VoxelTycoon.Tools;
using VoxelTycoon.Notifications;

namespace VoxelTyconTestProject
{
    public class HomeCameraScreenshot: VoxelTycoon.Modding.Mod
    {

        protected override void OnGameStarting()
        {
            CameraToolManager.Initialize();
            
            var notificationTypeId = "camera_mod/camera_notification_action".GetHashCode();
            NotificationManager.Current.RegisterNotificationAction<CameraNotificationAction>(notificationTypeId);
        }

        protected override void OnGameStarted()
        {
            Toolbar.Current.AddButton(FontIcon.Ketizoloto(I.Settings1), "Debug info", new ToolToolbarAction(()=> new CameraModSettingsTool()));

            var cameraUI = GameObject.Find("ModernGameUI/Camera");
            var baseRect = cameraUI.GetComponent<RectTransform>();

            var GO = GameObject.Instantiate(cameraUI, cameraUI.transform.parent);
            var newRect = cameraUI.GetComponent<RectTransform>();
            newRect.anchoredPosition += new Vector2(baseRect.sizeDelta.x + 10, 0);
            GO.transform.SetAsFirstSibling();

            var tooltipGO = GO.GetComponentInChildren<VoxelTycoon.UI.TooltipTarget>(); 
            tooltipGO.Set(null, "Screenshoot mode", "toggle on/off");

            CameraToolManager.Current.SendNotification("parent:" + cameraUI.transform.parent.name);
            CameraToolManager.Current.SendNotification("anch.p camera UI:" + baseRect.anchoredPosition + " pos: " + cameraUI.transform.localPosition);
            CameraToolManager.Current.SendNotification("anch.p screenshot UI:" + newRect.anchoredPosition + " pos: " + GO.transform.localPosition);

            var decor = GO.GetComponentInChildren<VoxelTycoon.UI.Controls.ClickableDecorator>();
            decor.OnClick = (ped)=> 
            { 
                CameraToolManager.Current.Enabled = !CameraToolManager.Current.Enabled;
                CameraToolManager.Current.SendNotification("Screenshot active:" + CameraToolManager.Current.Enabled);
            };
        }

        protected override void OnUpdate()
        {
            
        }

        protected override void Read(StateBinaryReader reader)
        {
            CameraToolManager.Current.Enabled = reader.ReadBool();
        }

        protected override void Write(StateBinaryWriter writer)
        {
            writer.WriteBool(CameraToolManager.Current.Enabled);
        }
       
        private void ShowComponents(GameObject gameObject)
        {
            CameraToolManager.Current.SendNotification("GAME OBJECT: " + gameObject.name);

            if (gameObject != null)
            {
                foreach (var component in gameObject.GetComponents<Component>())
                {
                    CameraToolManager.Current.SendNotification("type : " + component.GetType().ToString());
                }

                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    ShowComponents(gameObject.transform.GetChild(i).gameObject);
                }
            }
        }

    }


    public class CameraToolManager: VoxelTycoon.Manager<CameraToolManager>
    {
        Resolution currentResolution;
        Resolution newResolution = new Resolution();
        RenderTexture renderTexture;

        float timeToAction = 10f;
        float currentTime;

        public bool Enabled { get; set; } = false;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            newResolution.width = 3840;
            newResolution.height = 2160;

            renderTexture = new RenderTexture(3840, 2160, 100, UnityEngine.Experimental.Rendering.DefaultFormat.HDR);

            currentTime = timeToAction;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (!Enabled)
                return;

            currentTime -= Time.deltaTime;

            if (currentTime <= 0)
            {
                currentTime = timeToAction;
                MakeScreenshot();
            }
        }


        private void MakeScreenshot()
        {           
            //currentResolution = VoxelTycoon.Settings.Current.Resolution;
            //VoxelTycoon.Settings.Current.Resolution.Value = newResolution;

            var regionCenter = VoxelTycoon.RegionManager.Current.HomeRegion.Center;
           
            //get new camera position and rotation
            Vector3 cameraPosition = new Vector3(regionCenter.X, 500, regionCenter.Z);
            Vector3 cameralookPosition = new Vector3(regionCenter.X, 0, regionCenter.Z);

            //get current position and rotation           
            Quaternion cameraDefaultRotation = CameraController.Current.Camera.transform.localRotation;
            Vector3 cameraDefaultPosition    = CameraController.Current.transform.position;

            //add settings to camera
            CameraController.Current.ToggleOrthographic(true);
            CameraController.Current.transform.position = cameraPosition;
            CameraController.Current.Camera.transform.LookAt(cameralookPosition, Vector3.up);
            CameraController.Current.Camera.targetTexture = renderTexture;
            CameraController.Current.Camera.Render();
            CameraController.Current.Camera.targetTexture = null;
            SaveTexture();
            //revert settings
            CameraController.Current.ToggleOrthographic(false);
            CameraController.Current.transform.position = cameraDefaultPosition;
            CameraController.Current.Camera.transform.localRotation = cameraDefaultRotation;
           
            //set resolution back
            //VoxelTycoon.Settings.Current.Resolution.Value = currentResolution;

            SendNotification("Screenshot at " + System.DateTime.Now);
        }

        public void SaveTexture()
        {
            byte[] bytes = GetTexture().EncodeToPNG();
            System.IO.File.WriteAllBytes($"C:/Program Files (x86)/Steam/steamapps/common/VoxelTycoon/Content/VoxelTyconTestProject/Screenshot.png", bytes);
        }
        private Texture2D GetTexture()
        {
            Texture2D tex = new Texture2D(3840, 2160, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();
            return tex;
        }


        public void SendNotification(string text)
        {
            var priority = NotificationPriority.Critical;

            // Make the notification look fancy by setting the color
            // to the current company color.
            var color = Company.Current.Color;

            var title = "Hello World!";
            var message = text;

            // Action is executed when player clicks on notification.
            var action = new CameraNotificationAction();

            // If you don't need any action, just pass default value (null).
            // var action = default(INotificationAction);

            // Use custom FontAwesome (https://fontawesome.com/icons) icon
            var icon = FontIcon.FaSolid("\uf7e4");

            // And finally, call the API
            NotificationManager.Current.Push(priority, color, title, message, action, icon);
        }

    }


    public class CameraModSettingsTool : ITool
    {

        public void Activate()
        {
            var cameraUI = GameObject.Find("ModernGameUI/Camera");
            ShowComponents(cameraUI);

            CameraToolManager.Current.SendNotification("------------------");
            ShowComponents(cameraUI.transform.parent.gameObject, false);

        }

        private void ShowComponents(GameObject gameObject, bool withChilds = true)
        {
            CameraToolManager.Current.SendNotification("UI camera: " + gameObject.name);

            if (gameObject != null)
            {
                foreach (var component in gameObject.GetComponents<Component>())
                {
                    CameraToolManager.Current.SendNotification("type : " + component.GetType().ToString());
                }


                if (withChilds)
                {
                    for (int i = 0; i < gameObject.transform.childCount; i++)
                    {
                        ShowComponents(gameObject.transform.GetChild(i).gameObject);
                    }
                }
                            
            }
        }

        public bool Deactivate(bool soft)
        {
            return TryDeactivate();
        }

        public bool OnUpdate()
        {
            //if (ToolHelper.IsHotkeyDown(_toggleHotkey))
            //    CameraToolManager.Current.Enabled ^= true;

            //_toggleHotkeyPanelItem.SetCaption(CameraToolManager.Current.Enabled ? "Disable" : "Enable");

            

            


            return false;
        }

        public bool TryDeactivate()
        {
            //HotkeyPanel.Current.Clear();

            return true;
        }

    }


    public class CameraNotificationAction : INotificationAction
    {
        public void Act()
        {
            OverlayMessage.ShowAtScreenCenter("Super!!");
        }

        public void Read(StateBinaryReader reader)
        {
        }

        public void Write(StateBinaryWriter writer)
        {
        }
    }


}
