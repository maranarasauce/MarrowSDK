using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SLZ.Marrow.Warehouse;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SLZ.MarrowEditor;
using SLZ.Marrow;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;

namespace Maranara.Marrow
{
    public static class ElixirMixer
    {
        private static string ML_DIR = null;
        public static void ExportFlasks(Pallet pallet)
        {
            List<Flask> flasks = new List<Flask>();
            foreach (Crate crate in pallet.Crates)
            {
                if (crate.GetType().IsAssignableFrom(typeof(Flask)))
                {
                    Flask flask = (Flask)crate;
                    flasks.Add(flask);
                }
            }

            string palletPath = Path.GetFullPath(ModBuilder.BuildPath);
            string flaskPath = Path.Combine(palletPath, "flasks");

            if (!Directory.Exists(flaskPath))
                Directory.CreateDirectory(flaskPath);

            foreach (Flask flask in flasks)
            {
                string title = MarrowSDK.SanitizeName(flask.Title);
                title = Regex.Replace(title, @"\s+", "");
                bool success = ExportElixirs(title, flaskPath, flask);
                if (!success)
                    break;
            }
        }

        public static bool ExportElixirs(string title, string outputDirectory, Flask flask)
        {
            if (!ConfirmMelonDirectory())
                return false;

            List<string> exportedScriptPaths = new List<string>();

            string tempDir = Path.Combine(Path.GetTempPath(), title);
            Directory.CreateDirectory(tempDir);

            FlaskLabel label = (FlaskLabel)flask.MainAsset.EditorAsset;
            foreach (MonoScript type in label.Elixirs)
            {
                string path = AssetDatabase.GetAssetPath(type);
                exportedScriptPaths.Add(path);
                Debug.Log(path);
            }

            AssemblyBuilder asmBuilder = new AssemblyBuilder(Path.Combine(outputDirectory, title + ".dll"), exportedScriptPaths.ToArray());
            Debug.Log(asmBuilder.assemblyPath);
            asmBuilder.buildTarget = BuildTarget.StandaloneWindows64;
            asmBuilder.buildTargetGroup = BuildTargetGroup.Standalone;
            asmBuilder.buildFinished -= AsmBuilder_buildFinished;
            asmBuilder.buildFinished += AsmBuilder_buildFinished;
            return asmBuilder.Build();
        }

        private static void AsmBuilder_buildFinished(string arg1, CompilerMessage[] arg2)
        {
            Debug.Log("build finish");
            //asmBuilder.buildFinished -= AsmBuilder_buildFinished;
        }

        private static bool ConfirmMelonDirectory()
        {
            if (string.IsNullOrEmpty(ML_DIR))
            {
                bool solved = false;
                foreach (var gamePath in ModBuilder.GamePathDictionary)
                {
                    string gamePathSS = Path.Combine(gamePath.Value, "cauldronsave.txt");

                    if (File.Exists(gamePathSS))
                    {
                        string mlPath = File.ReadAllText(gamePathSS);
                        ML_DIR = mlPath.Replace("\n", "").Replace("\r", "");
                        solved = true;
                    }
                    else
                        continue;
                }

                if (!solved)
                {
                    EditorUtility.DisplayDialog("Help me out!", "Your MelonLoader directory isn't set. Please launch BONELAB with the MarrowCauldron mod at least once.", "Sure thing");
                    return false;
                }
            }
            return true;
        }

        /*public static bool ExportElixirsOLD(string title, string outputDirectory, Flask flask)
        {
            ConfirmMelonDirectory();

            List<Type> exportedTypes = new List<Type>();

            string tempDir = Path.Combine(Path.GetTempPath(), title);
            Directory.CreateDirectory(tempDir);

            // not very proud of this but hey if it works it works
            string projTemplateDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BehaviourProjectTemplate");

            XDocument csproj = XDocument.Parse(File.ReadAllText(Path.Combine(projTemplateDir, "CustomMonoBehaviour.csproj")).Replace("$safeprojectname$", title).Replace("$BONELAB_DIR$", ML_DIR));
            XElement compile = csproj.Root.Elements().Single((e) => e.ToString().Contains("Compile"));

            var newScriptFile = File.ReadAllLines(Path.Combine(projTemplateDir, "CustomMonoBehaviour.cs")).ToList();
            for (int i = 0; i < newScriptFile.Count; i++) newScriptFile[i] = newScriptFile[i].Replace("$safeprojectname$", title);
            int lastIndex = newScriptFile.IndexOf(newScriptFile.Single((s) => s.Contains("newshithere")));

            string[] scriptFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            FlaskLabel label = (FlaskLabel)flask.MainAsset.EditorAsset;
            foreach (Type type in label.Elixirs)
            {
                if (exportedTypes.Contains(type))
                {
                    Debug.Log("Found duplicate script, skipping");
                    continue;
                }

                Debug.Log("Searching for elixir of " + type.Name);
                string scriptPath = scriptFiles.FirstOrDefault((f) => Path.GetFileNameWithoutExtension(f) == type.Name);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    XElement newCompile = new XElement("Compile");
                    newCompile.SetAttributeValue("Include", Path.GetFileName(scriptPath));
                    compile.Add(newCompile);

                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(scriptPath));
                    CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                    ClassDeclarationSyntax rootClass = null;

                    // Add the IntPtr constructor for UnhollowerRuntimeLib
                    bool inptrable = type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsSubclassOf(typeof(ScriptableObject));

                    DontAssignIntPtr dontAssignIntPtr = (DontAssignIntPtr)type.GetCustomAttribute(typeof(DontAssignIntPtr));
                    if (dontAssignIntPtr != null)
                        inptrable = false;

                    if (inptrable)
                    {
                        ConstructorDeclarationSyntax ptrConstructor = SyntaxFactory.ConstructorDeclaration(type.Name).WithInitializer
                        (
                            ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                                .AddArgumentListArguments(Argument(IdentifierName("intPtr")))
                        ).WithBody(Block());
                        ptrConstructor = ptrConstructor.AddParameterListParameters(Parameter(List<AttributeListSyntax>(), TokenList(), ParseTypeName("System.IntPtr"), Identifier("intPtr"), null));
                        ptrConstructor = ptrConstructor.AddModifiers(Token(SyntaxKind.PublicKeyword));
                        ptrConstructor = ptrConstructor.NormalizeWhitespace();
                        rootClass = MixerLibs.UpdateMainClass(root, type.Name);
                        root = root.ReplaceNode(rootClass, rootClass.AddMembers(ptrConstructor));
                    }

                    // Remove all attributes using a rewriter class
                    root = new MixerLibs.AttributeRemoverRewriter().Visit(root).SyntaxTree.GetCompilationUnitRoot();

                    // Convert the final script to a string and switch UnityAction for System.Action
                    string finalScript = root.NormalizeWhitespace().ToFullString();
                    finalScript = finalScript.Replace("[Elixir]", "");
                    finalScript = finalScript.Replace("[DontAssignIntPtr]", "");
                    finalScript = finalScript.Replace("new UnityAction", "new System.Action");
                    finalScript = finalScript.Replace("new UnityEngine.Events.UnityAction", "new System.Action");
                    // Swap StartCoroutine for MelonCoroutines.Start
                    finalScript = finalScript.Replace("this.StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");
                    finalScript = finalScript.Replace("base.StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");
                    finalScript = finalScript.Replace("StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");

                    File.WriteAllText(Path.Combine(tempDir, Path.GetFileName(scriptPath)), finalScript);
                    //newScriptFile.Insert(lastIndex += 1, $"CustomMonoBehaviourHandler.RegisterMonoBehaviourInIl2Cpp<{type.Name}>();");

                    exportedTypes.Add(type);
                }
                else
                    Debug.LogError("FAILED TO FIND SCRIPT FOR " + type.Name + ". SKIPPING");
            }

            // xml stuff is weird so uh heres this
            string finalCsproj = csproj.ToString().Replace("xmlns=\"\" ", "");
            File.WriteAllText(Path.Combine(tempDir, "CustomMonoBehaviour.csproj"), finalCsproj);
            File.WriteAllLines(Path.Combine(tempDir, "CustomMonoBehaviour.cs"), newScriptFile);
            File.WriteAllText(Path.Combine(tempDir, "AssemblyInfo.cs"), File.ReadAllText(Path.Combine(projTemplateDir, "AssemblyInfo.cs")).Replace("$safeprojectname$", title));

            MSBuildBuildProfile profile = MSBuildBuildProfile.Create("Debug", false, "-t:Build -p:Configuration=Debug");
            List<MSBuildBuildProfile> profileList = new List<MSBuildBuildProfile>();
            profileList.Add(profile);
            IEnumerable<MSBuildBuildProfile> profiles = profileList;

            MSBuildProjectReference project = MSBuildProjectReference.FromMSBuildProject(Path.Combine(tempDir, "CustomMonoBehaviour.csproj"), profiles: profiles);

            try
            {
                project.BuildProject(profile.Name);

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                if (File.Exists(Path.Combine(outputDirectory, $"{title}.dll")))
                    File.Delete(Path.Combine(outputDirectory, $"{title}.dll"));

                File.Copy(Path.Combine(tempDir, "bin", "Debug", title + ".dll"), Path.Combine(outputDirectory, $"{title}.dll"));
                Directory.Delete(tempDir, true);
                return true;

            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("ERROR", "Your compiled scripts had errors. Opening the output log...", "OK");
                string msbuild = $"{System.IO.Directory.GetCurrentDirectory()}\\msbuild_out.txt";
                Debug.Log(msbuild);
                System.Diagnostics.Process.Start(msbuild);
                throw e;
                return false;
            }
        }*/
    }

}
