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
using System;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace Maranara.Marrow
{
    public static class ElixirMixer
    {
        public static string ML_DIR = null;
        public static string ML_MANAGED_DIR = null;
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

            string tempDir = Path.Combine(Application.dataPath, $"{GUID.Generate()}-{title}");
            Directory.CreateDirectory(tempDir);

            FlaskLabel label = (FlaskLabel)flask.MainAsset.EditorAsset;
            foreach (MonoScript type in label.Elixirs)
            {
                string path = AssetDatabase.GetAssetPath(type);
                string newPath = Path.Combine(tempDir, Path.GetFileName(path));

                CreateTempElixir(newPath, type.text, type.GetClass());

                exportedScriptPaths.Add(newPath);
            }

            AssemblyBuilder asmBuilder = new AssemblyBuilder(Path.Combine(outputDirectory, title + ".dll"), exportedScriptPaths.ToArray());

            asmBuilder.buildTarget = BuildTarget.StandaloneWindows64;
            asmBuilder.buildTargetGroup = BuildTargetGroup.Standalone;
            asmBuilder.buildFinished -= ((arg1, arg2) => AsmBuilder_buildFinished(arg1, arg2, tempDir, title));
            asmBuilder.buildFinished += ((arg1, arg2) => AsmBuilder_buildFinished(arg1, arg2, tempDir, title));

            asmBuilder.excludeReferences = asmBuilder.defaultReferences;

            List<string> references = new List<string>();

            if (label.useDefaultIngredients)
                references.AddRange(GetDefaultReferences(true));
            else references.AddRange(AddPathToReferences(label.ingredients));

            if (label.additionalIngredients != null)
                references.AddRange(AddPathToReferences(label.additionalIngredients));

            asmBuilder.additionalReferences = references.ToArray();
            asmBuilder.compilerOptions = new ScriptCompilerOptions()
            {
                CodeOptimization = CodeOptimization.Release
            };

            return asmBuilder.Build();
        }

        #region ReferenceUtils
        public static string[] GetDefaultReferences(bool withPath)
        {
            ConfirmMelonDirectory();

            if (!withPath)
            {
                return GetDefaultReferencesNoPath();
            }

            List<string> additionalReferences = new List<string>();
            additionalReferences.Add(Path.Combine(ML_DIR, "MelonLoader.dll"));
            additionalReferences.Add(Path.Combine(ML_DIR, "0Harmony.dll"));
            foreach (string reference in Directory.GetFiles(ML_MANAGED_DIR))
            {
                if (!reference.EndsWith(".dll"))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!(fileName == "netstandard"))
                {
                    additionalReferences.Add(reference);
                }
            }
            return additionalReferences.ToArray();
        }

        private static string[] GetDefaultReferencesNoPath()
        {
            List<string> additionalReferences = new List<string>();
            additionalReferences.Add("..\\MelonLoader.dll");
            additionalReferences.Add("..\\0Harmony.dll");
            foreach (string reference in Directory.GetFiles(ML_MANAGED_DIR))
            {
                if (!reference.EndsWith(".dll"))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!(fileName == "netstandard"))
                {
                    additionalReferences.Add(fileName);
                }
            }
            return additionalReferences.ToArray();
        }

        private static string[] AddPathToReferences(string[] references)
        {
            for (int i = 0; i < references.Length; i++)
            {
                string path = references[i];
                if (!File.Exists(path))
                {
                    string newPath = Path.Combine(ML_MANAGED_DIR, path); ;
                    if (File.Exists(newPath))
                        references[i] = newPath;
                }
            }
            return references;
        }
        #endregion

        private static void CreateTempElixir(string path, string allText, Type elixirClass)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(allText);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
            ClassDeclarationSyntax rootClass = null;

            // Add the IntPtr constructor for UnhollowerRuntimeLib
            bool inptrable = elixirClass.IsSubclassOf(typeof(MonoBehaviour)) || elixirClass.IsSubclassOf(typeof(ScriptableObject));

            DontAssignIntPtr dontAssignIntPtr = (DontAssignIntPtr)elixirClass.GetCustomAttribute(typeof(DontAssignIntPtr));
            if (dontAssignIntPtr != null)
                inptrable = false;

            if (inptrable)
            {
                ConstructorDeclarationSyntax ptrConstructor = SyntaxFactory.ConstructorDeclaration(elixirClass.Name).WithInitializer
                (
                    SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("intPtr")))
                ).WithBody(SyntaxFactory.Block());
                ptrConstructor = ptrConstructor.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.List<AttributeListSyntax>(), SyntaxFactory.TokenList(), SyntaxFactory.ParseTypeName("System.IntPtr"), SyntaxFactory.Identifier("intPtr"), null));
                ptrConstructor = ptrConstructor.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                ptrConstructor = ptrConstructor.NormalizeWhitespace();
                rootClass = MixerLibs.UpdateMainClass(root, elixirClass.Name);
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

            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write(finalScript);
            }
        }

        private static void AsmBuilder_buildFinished(string arg1, CompilerMessage[] arg2, string tempDir, string title)
        {
            bool hasErrors = false;

            foreach (CompilerMessage msg in arg2)
            {
                switch (msg.type)
                {
                    case CompilerMessageType.Info:
                        Debug.Log(msg.message);
                        break;
                    case CompilerMessageType.Error:
                        hasErrors = true;
                        Debug.LogError(msg.message);
                        break;
                    case CompilerMessageType.Warning:
                        Debug.LogWarning(msg.message);
                        break;
                }
                
            }

            if (hasErrors)
            {
                EditorUtility.DisplayDialog($"Errors found in {title}", $"There was an Error detected while building {title}! Please check the Console.", "Okay");
            }

            foreach (string file in Directory.GetFiles(tempDir))
            {
                File.Delete(file);
            }
            Directory.Delete(tempDir);
        }

        public static bool ConfirmMelonDirectory()
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
                        ML_MANAGED_DIR = Path.Combine(ML_DIR, "Managed");
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
    }

}
