using CommandProviderInterface;
using EditorCore.Buffer;
using EditorCore.File;
using Lsp;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorCore.Server
{
    public class EditorServer
    {
        public ICommandProvider CommandProvider { get; internal set; }
        public Lock FilesLock = new();
        public List<EditorFile> Files { get; internal set; }

        Dictionary<string, Task<LspClient>> clients = [];
        public EditorBufferOnUpdate? ActionOnBufferUpdate;
        public EditorBufferOnTextInput? ActionOnBufferTextInput;
        public EditorFileOnSave? ActionOnFileSave;
        public int OpeningFiles = 0;

        public bool UseLSP { get; set; }

        public EditorServer(ICommandProvider commandProvider)
        {
            CommandProvider = commandProvider;
            Files = [];
        }

        public EditorFile OpenFile(string filename)
        {
            EditorFile new_file = new(this, filename, new PersistentCTextBuffer(filename));
            using (FilesLock.EnterScope())
            {
                Files.Add(new_file);
                OpeningFiles--;
            }
            return new_file;
        }

        public EditorFile CreateFile(string? name, string? languageId)
        {
            EditorFile new_file = new(this, new EditorBuffer(this, BaseTokenizer.CreateTokenizer(languageId), name, languageId, new PersistentCTextBuffer()))
            {
                filename = name
            };
            using (FilesLock.EnterScope())
            {
                Files.Add(new_file);
                OpeningFiles--;
            }
            return new_file;
        }

        public Task<LspClient>? GetLspAsync(string? languageId)
        {
            if (!UseLSP) return null;
            if (languageId != null && clients.TryGetValue(languageId, out var value))
            {
                return value;
            }
            switch (languageId)
            {
                case "c":
                    // // This don't works :(
                    //clients[languageId] = LspClient.StartAsync(Environment.CurrentDirectory,
                    //                                           "clangd",
                    //                                           "--offset-encoding=utf-8 --background-index --clang-tidy",
                    //                                           new Dictionary<string, object>
                    //                                           {
                    //                                               {"clangTidy", true },
                    //                                               {"clangd.config", @"
                    //                                                    Diagnostics:
                    //                                                      ClangTidy:
                    //                                                        Add: ['*']
                    //                                                        # Remove: [altera*, abseil*, fuchsia*]
                    //                                                    CompileFlags:
                    //                                                      Add: [
                    //                                                        -Weverything, 
                    //                                                        -fsanitize=undefined, 
                    //                                                        -D_CRT_SECURE_NO_WARNINGS, 
                    //                                                        -D_CRT_NONSTDC_NO_DEPRECATE, 
                    //                                                        -fms-extensions, 
                    //                                                        -Wno-microsoft,
                    //                                                        -Wno-c++98-compat,
                    //                                                        -Wno-pre-c11-compat
                    //                                                      ]
                    //                                                    ---
                    //                                                    If:
                    //                                                      PathMatch: .*\.(c|h)$
                    //                                                    CompileFlags:
                    //                                                      Add: [-std=gnu2y]
                    //                                                    ---
                    //                                                    If:
                    //                                                      PathMatch: .*\.(cpp|cc|cxx|hpp|hxx)$
                    //                                                    CompileFlags:
                    //                                                      Add: [-std=gnu++2c]
                    //                                                    "
                    //                                           } });
                    clients[languageId] = LspClient.StartAsync(Environment.CurrentDirectory,
                                                               "clangd",
                                                               "--offset-encoding=utf-8 --background-index --clang-tidy",
                                                               new
                                                               {
                                                                   fallbackFlags = new[] { "-Weverything", "-Wno-empty-translation-unit", "-fsanitize=undefined", "-D_CRT_SECURE_NO_WARNINGS", "-D_CRT_NONSTDC_NO_DEPRECATE", "-fms-extensions", "-Wno-microsoft", "-Wno-extension", "-Wno-c99-extensions", "-Wno-c++11-extensions", "-Wno-c++11-compat", "-Wno-declaration-after-statement" },
                                                                   clangTidy = true,
                                                                   clangTidyChecks = "*"
                                                               });
                    break;
                case "powershell":
                    clients[languageId] = LspClient.StartAsync(Environment.CurrentDirectory,
                                                               "pwsh",
                                                               "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"C:/path_dir/PowerShellEditorServices/PowerShellEditorServices/Start-EditorServices.ps1 -Stdio -LogPath ./pses.log -SessionDetailsPath ./session.json -FeatureFlags @()\"",
                                                               new());
                    break;
                default:
                    return null;
            };
            return clients[languageId];
        }

        public void CloseFile(EditorFile file)
        {
            using (FilesLock.EnterScope())
            {
                Files.Remove(file);
                file.Dispose();
            }
        }

        /* declarations for simplicity */
    }
}
