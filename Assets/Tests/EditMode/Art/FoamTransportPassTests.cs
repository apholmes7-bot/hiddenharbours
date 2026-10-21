using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportPassTests
    {
        const string ShaderPath="Assets/_Project/Art/Shaders/HiddenHarboursFoamTransport.shader";
        static string Source(string path) => File.ReadAllText(Path.Combine(Application.dataPath,"..",path));

        [Test] public void FoamTransportShader_Imports_WithNoCompileErrors()
        {
            bool previous=LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages=true;
                AssetDatabase.ImportAsset(ShaderPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                Assert.IsNotNull(shader);
                var errors=new StringBuilder();
                foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    if(message.severity==ShaderCompilerMessageSeverity.Error) errors.AppendLine(message.message);
                Assert.IsEmpty(errors.ToString());
                Assert.IsNotNull(Shader.Find("Hidden/HiddenHarbours/FoamTransport"));
                foreach(string pass in new[]{"F2XEven","F2XOdd","F2YEven","F2YOdd"})
                    StringAssert.Contains("Name \""+pass+"\"",Source(ShaderPath));
            }
            finally { LogAssert.ignoreFailingMessages=previous; }
        }

        [Test] public void FoamTransport_NoHistorySamplingOrNewClock()
        {
            string shader=Source(ShaderPath);
            StringAssert.Contains("_F2Prev.Load",shader);
            Assert.IsFalse(Regex.IsMatch(shader,@"_F2Prev\s*\.\s*Sample"));
            StringAssert.DoesNotContain("SAMPLE_TEXTURE",shader);
            StringAssert.Contains("#define F2_CODES 65535u",shader);
            StringAssert.Contains("#define F2_MAX_COURANT 0.25",shader);
            foreach(string name in new[]{"FoamTransport","FoamTransportField","FoamTransportContacts","FoamTransportContact"})
            {
                string code=Source("Assets/_Project/Code/Art/"+name+".cs");
                Assert.IsFalse(Regex.IsMatch(code,@"\bTime\s*\.|\bRandom\s*\."),name);
            }
        }

        [Test] public void FoamTransport_RepeatedRenderDoesNotStep()
        {
            Type passType=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic);
            Type stateType=passType.GetNestedType("FoamState",BindingFlags.NonPublic);
            object pass=Activator.CreateInstance(passType,new object[]{null}), state=Activator.CreateInstance(stateType,true);
            var route=stateType.GetField("TransportRoute"); var remainder=stateType.GetField("DriftResidual");
            route.SetValue(state,FoamTransport.Route.Local); remainder.SetValue(state,new Vector2(.04f,.07f));
            try
            {
                var method=passType.GetMethod("PrepareTransport",BindingFlags.Instance|BindingFlags.NonPublic);
                var step=(FoamTransport.Step)method.Invoke(pass,new object[]{state,96f,Vector2.zero,0f,true});
                Assert.AreEqual(0,step.Count); Assert.AreEqual(FoamTransport.Route.Local,route.GetValue(state));
                Assert.AreEqual(new Vector2(.04f,.07f),remainder.GetValue(state));
                Assert.IsNull(stateType.GetField("Transport").GetValue(state));
            }
            finally { passType.GetMethod("Dispose").Invoke(pass,null); }
        }

        [Test] public void FoamTransport_ReallocationReleasesAuxiliaryState()
        {
            Type passType=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic);
            Type stateType=passType.GetNestedType("FoamState",BindingFlags.NonPublic);
            object state=Activator.CreateInstance(stateType,true);
            var field=new FoamTransportField(8,1);
            var texture=field.Mask;
            stateType.GetField("Transport").SetValue(state,field);
            var init=(FoamHistoryInitialization)stateType.GetField("Initialization").GetValue(state);
            var old=init.State.Begin();
            stateType.GetMethod("Release").Invoke(state,null);
            Assert.IsNull(stateType.GetField("Transport").GetValue(state));
            Assert.IsTrue(texture==null,"Auxiliary texture ownership must end with history allocation.");
            Assert.IsFalse(init.State.Confirm(old,true));
            Assert.IsTrue(init.RequiresRecording);
        }

        [Test] public void FoamTransport_RenderGraphDeclaresAndOrdersResources()
        {
            string code=Source("Assets/_Project/Code/Art/IsoFacetHullFeature.cs");
            int readiness=code.IndexOf("cameraFoam.Initialization.CanRecord()",StringComparison.Ordinal);
            int prepare=code.IndexOf("FoamTransport.Step transportStep = PrepareTransport",StringComparison.Ordinal);
            Assert.Less(readiness,prepare); Assert.Greater(readiness,0);
            StringAssert.Contains("builder.UseTexture(flow, AccessFlags.Read)",code);
            StringAssert.Contains("builder.UseTexture(mask, AccessFlags.Read)",code);
            StringAssert.Contains("matching < FoamTransport.Matchings",code);
            StringAssert.Contains("FoamInjectionRegistry.DriftVelocity * dt",code);
        }

        [Test] public void FoamTransport_IdleAndRemovedCamerasReleaseOnlyOwnedAuxiliaries()
        {
            Type passType=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic);
            Type stateType=passType.GetNestedType("FoamState",BindingFlags.NonPublic);
            object pass=Activator.CreateInstance(passType,new object[]{null});
            var states=(IDictionary)passType.GetField("_foamStates",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pass);
            var release=passType.GetMethod("ReleaseTransport",BindingFlags.NonPublic|BindingFlags.Instance);
            var a=new GameObject("F2 live camera"); var b=new GameObject("F2 removed camera");
            object first=Activator.CreateInstance(stateType,true), second=Activator.CreateInstance(stateType,true);
            var firstField=new FoamTransportField(8,1); var secondField=new FoamTransportField(8,1);
            var firstTexture=firstField.Mask; var secondTexture=secondField.Mask;
            try
            {
                stateType.GetField("Owner").SetValue(first,a.AddComponent<Camera>());
                stateType.GetField("Owner").SetValue(second,b.AddComponent<Camera>());
                stateType.GetField("Transport").SetValue(first,firstField);
                stateType.GetField("Transport").SetValue(second,secondField);
                states.Add(a.GetEntityId(),first); states.Add(b.GetEntityId(),second);
                release.Invoke(pass,new object[]{false});
                Assert.IsTrue(firstTexture!=null && secondTexture!=null);
                UnityEngine.Object.DestroyImmediate(b);
                release.Invoke(pass,new object[]{false});
                Assert.IsTrue(firstTexture!=null); Assert.IsTrue(secondTexture==null);
                release.Invoke(pass,new object[]{true});
                Assert.IsTrue(firstTexture==null,"Idle/off releases the remaining auxiliary allocation.");
            }
            finally
            {
                passType.GetMethod("Dispose").Invoke(pass,null);
                UnityEngine.Object.DestroyImmediate(a); if(b!=null) UnityEngine.Object.DestroyImmediate(b);
            }
        }
    }
}
