#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public static void BakeLandingPropsFromCommandLine()
        {
            int code=1;
            try
            {
                StPetersLandingPropsBaker.BakeImportBuild();
                ApplyKeyScenesPatchBatch();
                string[] args=System.Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-landingPropsResult");
                if(at<0||at+1>=args.Length)throw new InvalidOperationException("Landing props result path required.");
                File.WriteAllText(args[at+1],"LANDING_PROPS_BAKE_APPLY_SUCCESS\n");
                code=0;
            }
            catch(Exception ex){Debug.LogException(ex);}
            EditorApplication.Exit(code);
        }
    }
}
#endif
