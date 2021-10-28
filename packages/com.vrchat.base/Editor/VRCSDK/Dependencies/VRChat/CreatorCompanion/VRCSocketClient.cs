 using UnityEditor;
 using VRC.PackageManagement;

 [InitializeOnLoad]
 public class VRCSocketClient
 {

     private static UnityWindowClient _client = null;
     
     static VRCSocketClient ()
     {
         if (_client != null)
         {
             _client.Disconnect();
         }

         _client = new UnityWindowClient();
     }
 }