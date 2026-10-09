using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraLoom.Integration
{
    /// <summary>Small consumer-only walk test; not a dependency of the three product packages.</summary>
    public sealed class IntegrationTestDriver : MonoBehaviour
    {
        public TerraLoomIntegration Composition;
        public Camera View;
        private CharacterController player;
        private Vector3 overviewPosition;
        private Quaternion overviewRotation;
        private float yaw=90,pitch,vertical;
        private bool walking;
        public bool Walking => walking;
        private void Start()
        { if (!View) View=Camera.main; if (View) { overviewPosition=View.transform.position; overviewRotation=View.transform.rotation; } }
        public void BeginWalk()
        {
            if (!View || !Composition || !Composition.World || !Composition.World.Terrain) return;
            if (!player)
            {
                var host=new GameObject("Walk test player");player=host.AddComponent<CharacterController>();
                player.height=1.8f;player.radius=.3f;player.stepOffset=.25f;player.slopeLimit=40;
            }
            var terrain=Composition.World.Terrain;
            var spawn=terrain.transform.position+new Vector3(12,0,48);spawn.y=terrain.SampleHeight(spawn)+terrain.transform.position.y+1;
            player.enabled=false;player.transform.position=spawn;player.enabled=true;
            walking=true;yaw=90;pitch=0;vertical=0;
            View.transform.SetParent(player.transform,false);View.transform.localPosition=new Vector3(0,.6f,0);View.transform.localRotation=Quaternion.Euler(0,yaw,0);
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        public void EndWalk()
        {
            walking=false;
            if (View) { View.transform.SetParent(null,true);View.transform.SetPositionAndRotation(overviewPosition,overviewRotation); }
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if (player) { Destroy(player.gameObject);player=null; }
        }
        private void Update()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if (keyboard.tabKey.wasPressedThisFrame) { if(walking)EndWalk();else BeginWalk(); }
            if (!walking || !player) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { EndWalk();return; }
            if(Mouse.current!=null)
            { var delta=Mouse.current.delta.ReadValue();yaw+=delta.x*.08f;pitch=Mathf.Clamp(pitch-delta.y*.08f,-85,85); }
            View.transform.localRotation=Quaternion.Euler(pitch,yaw,0);
            float x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),z=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
            var move=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1)*4;
            if(player.isGrounded) { vertical=-2;if(keyboard.spaceKey.wasPressedThisFrame)vertical=5; }
            vertical-=18*Time.deltaTime;player.Move((move+Vector3.up*vertical)*Time.deltaTime);
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15,15,460,135),GUI.skin.box);
            GUILayout.Label("TerraLoom — Core / Rivers / Paths test");
            GUILayout.Label(walking?"WASD move · mouse look · Space jump · Esc overview":"Tab: walk over the path, ramps and bridge");
            if (Composition) GUILayout.Label(Composition.Diagnostics);
            if(GUILayout.Button(walking?"Return to overview":"Walk from western path target")) { if(walking)EndWalk();else BeginWalk(); }
            GUILayout.EndArea();
        }
        private void OnDestroy()
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if (player) Destroy(player.gameObject);
        }
    }
}
