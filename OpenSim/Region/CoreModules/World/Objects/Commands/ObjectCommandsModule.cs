/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Mono.Addins;
using NDesk.Options;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Objects.Commands
{
    /// <summary>
    /// A module that holds commands for manipulating objects in the scene.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ObjectCommandsModule")]
    public class ObjectCommandsModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                

        private Scene m_scene;
        private ICommandConsole m_console;

        public string Name { get { return "Object Commands Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

            m_scene = scene;
            m_console = MainConsole.Instance;

            m_console.Commands.AddCommand(
                "Objects", false, "delete object owner",
                "delete object owner <UUID>",
                "Delete scene objects by owner",
                "Command will ask for confirmation before proceeding.",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object creator",
                "delete object creator <UUID>",
                "Delete scene objects by creator",
                "Command will ask for confirmation before proceeding.",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object uuid",
                "delete object uuid <UUID>",
                "Delete a scene object by uuid",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object name",
                "delete object name [--regex] <name>",
                "Delete a scene object by name.",
                "Command will ask for confirmation before proceeding.\n"
                  + "If --regex is specified then the name is treatead as a regular expression",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object outside",
                "delete object outside",
                "Delete all scene objects outside region boundaries",
                "Command will ask for confirmation before proceeding.",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "delete object pos",
                "delete object pos <start-coord> to <end-coord>",
                "Delete scene objects within the given area.",
                ConsoleUtil.CoordHelp,
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object uuid",
                "show object uuid [--full] <UUID>",
                "Show details of a scene object with the given UUID",
                "The --full option will print out information on all the parts of the object.\n"
                    + "For yet more detailed part information, use the \"show part\" commands.",
                HandleShowObjectByUuid);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object name",
                "show object name [--full] [--regex] <name>",
                "Show details of scene objects with the given name.",
                "The --full option will print out information on all the parts of the object.\n"
                    + "For yet more detailed part information, use the \"show part\" commands.\n"
                    + "If --regex is specified then the name is treatead as a regular expression.",
                HandleShowObjectByName);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object pos",
                "show object pos [--full] <start-coord> to <end-coord>",
                "Show details of scene objects within the given area.",
                "The --full option will print out information on all the parts of the object.\n"
                    + "For yet more detailed part information, use the \"show part\" commands.\n"
                    + ConsoleUtil.CoordHelp,
                HandleShowObjectByPos);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part uuid",
                "show part uuid <UUID>",
                "Show details of a scene object parts with the given UUID", HandleShowPartByUuid);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part name",
                "show part name [--regex] <name>",
                "Show details of scene object parts with the given name.",
                "If --regex is specified then the name is treatead as a regular expression",
                HandleShowPartByName);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part pos",
                "show part pos <start-coord> to <end-coord>",
                "Show details of scene object parts within the given area.",
                ConsoleUtil.CoordHelp,
                HandleShowPartByPos);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        /// <summary>
        /// Outputs the sogs to console.
        /// </summary>
        /// <param name='searchPredicate'></param>
        /// <param name='showFull'>If true then output all part details.  If false then output summary.</param>
        private void OutputSogsToConsole(Predicate<SceneObjectGroup> searchPredicate, bool showFull)
        {
            List<SceneObjectGroup> sceneObjects = m_scene.GetSceneObjectGroups().FindAll(searchPredicate);

            StringBuilder sb = new StringBuilder();

            foreach (SceneObjectGroup so in sceneObjects)
            {
                AddSceneObjectReport(sb, so, showFull);
                sb.Append("\n");
            }

            sb.AppendFormat("{0} object(s) found in {1}\n", sceneObjects.Count, m_scene.Name);

            m_console.OutputFormat(sb.ToString());
        }

        private void OutputSopsToConsole(Predicate<SceneObjectPart> searchPredicate, bool showFull)
        {
            List<SceneObjectGroup> sceneObjects = m_scene.GetSceneObjectGroups();
            List<SceneObjectPart> parts = new List<SceneObjectPart>();

            sceneObjects.ForEach(so => parts.AddRange(Array.FindAll<SceneObjectPart>(so.Parts, searchPredicate)));

            StringBuilder sb = new StringBuilder();

            foreach (SceneObjectPart part in parts)
            {
                AddScenePartReport(sb, part, showFull);
                sb.Append("\n");
            }

            sb.AppendFormat("{0} parts found in {1}\n", parts.Count, m_scene.Name);

            m_console.OutputFormat(sb.ToString());
        }

        private void HandleShowObjectByUuid(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            bool showFull = false;
            OptionSet options = new OptionSet().Add("full", v => showFull = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show object uuid <uuid>");
                return;
            }

            UUID objectUuid;
            if (!UUID.TryParse(mainParams[3], out objectUuid))
            {
                m_console.OutputFormat("{0} is not a valid uuid", mainParams[3]);
                return;
            }

            SceneObjectGroup so = m_scene.GetSceneObjectGroup(objectUuid);

            if (so == null)
            {
//                m_console.OutputFormat("No part found with uuid {0}", objectUuid);
                return;
            }

            StringBuilder sb = new StringBuilder();
            AddSceneObjectReport(sb, so, showFull);

            m_console.OutputFormat(sb.ToString());
        }

        private void HandleShowObjectByName(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            bool showFull = false;
            bool useRegex = false;
            OptionSet options = new OptionSet();
            options.Add("full", v => showFull = v != null );
            options.Add("regex", v => useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show object name [--full] [--regex] <name>");
                return;
            }

            string name = mainParams[3];

            Predicate<SceneObjectGroup> searchPredicate;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchPredicate = so => nameRegex.IsMatch(so.Name);
            }
            else
            {
                searchPredicate = so => so.Name == name;
            }

            OutputSogsToConsole(searchPredicate, showFull);
        }

        private void HandleShowObjectByPos(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            bool showFull = false;
            OptionSet options = new OptionSet().Add("full", v => showFull = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 5)
            {
                m_console.OutputFormat("Usage: show object pos [--full] <start-coord> to <end-coord>");
                return;
            }

            Vector3 startVector, endVector;

            if (!TryParseVectorRange(cmdparams.Skip(3).Take(3), out startVector, out endVector))
                return;

            Predicate<SceneObjectGroup> searchPredicate
                = so => Util.IsInsideBox(so.AbsolutePosition, startVector, endVector);

            OutputSogsToConsole(searchPredicate, showFull);
        }

        private void HandleShowPartByUuid(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

//            bool showFull = false;
            OptionSet options = new OptionSet();
//            options.Add("full", v => showFull = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show part uuid [--full] <uuid>");
                return;
            }

            UUID objectUuid;
            if (!UUID.TryParse(mainParams[3], out objectUuid))
            {
                m_console.OutputFormat("{0} is not a valid uuid", mainParams[3]);
                return;
            }

            SceneObjectPart sop = m_scene.GetSceneObjectPart(objectUuid);

            if (sop == null)
            {
//                m_console.OutputFormat("No part found with uuid {0}", objectUuid);
                return;
            }

            StringBuilder sb = new StringBuilder();
            AddScenePartReport(sb, sop, true);

            m_console.OutputFormat(sb.ToString());
        }

        private void HandleShowPartByPos(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

//            bool showFull = false;
            OptionSet options = new OptionSet();
//            options.Add("full", v => showFull = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 5)
            {
                m_console.OutputFormat("Usage: show part pos [--full] <start-coord> to <end-coord>");
                return;
            }

            string rawConsoleStartVector = mainParams[3];
            Vector3 startVector;

            if (!ConsoleUtil.TryParseConsoleMinVector(rawConsoleStartVector, out startVector))
            {
                m_console.OutputFormat("Error: Start vector {0} does not have a valid format", rawConsoleStartVector);
                return;
            }

            string rawConsoleEndVector = mainParams[5];
            Vector3 endVector;

            if (!ConsoleUtil.TryParseConsoleMaxVector(rawConsoleEndVector, out endVector))
            {
                m_console.OutputFormat("Error: End vector {0} does not have a valid format", rawConsoleEndVector);
                return;
            }

            OutputSopsToConsole(sop => Util.IsInsideBox(sop.AbsolutePosition, startVector, endVector), true);
        }

        private void HandleShowPartByName(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

//            bool showFull = false;
            bool useRegex = false;
            OptionSet options = new OptionSet();
//            options.Add("full", v => showFull = v != null );
            options.Add("regex", v => useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show part name [--full] [--regex] <name>");
                return;
            }

            string name = mainParams[3];

            Predicate<SceneObjectPart> searchPredicate;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchPredicate = sop => nameRegex.IsMatch(sop.Name);
            }
            else
            {
                searchPredicate = sop => sop.Name == name;
            }

            OutputSopsToConsole(searchPredicate, true);
        }

        /// <summary>
        /// Append a scene object report to an input StringBuilder
        /// </summary>
        /// <returns></returns>
        /// <param name='sb'></param>
        /// <param name='so'</param>
        /// <param name='showFull'>
        /// If true then information on all parts of an object is appended.
        /// If false then only summary information about an object is appended.
        /// </param>
        private StringBuilder AddSceneObjectReport(StringBuilder sb, SceneObjectGroup so, bool showFull)
        {
            if (showFull)
            {
                foreach (SceneObjectPart sop in so.Parts)
                {
                    AddScenePartReport(sb, sop, false);
                    sb.Append("\n");
                }
            }
            else
            {
                AddSummarySceneObjectReport(sb, so);
            }

            return sb;
        }

        private StringBuilder AddSummarySceneObjectReport(StringBuilder sb, SceneObjectGroup so)
        {
            ConsoleDisplayList cdl = new ConsoleDisplayList();
            cdl.AddRow("Name", so.Name);
            cdl.AddRow("Descrition", so.Description);
            cdl.AddRow("Local ID", so.LocalId);
            cdl.AddRow("UUID", so.UUID);
            cdl.AddRow("Location", string.Format("{0} @ {1}", so.AbsolutePosition, so.Scene.Name));
            cdl.AddRow("Parts", so.PrimCount);
            cdl.AddRow("Flags", so.RootPart.Flags);

            return sb.Append(cdl.ToString());
        }

        /// <summary>
        /// Append a scene object part report to an input StringBuilder
        /// </summary>
        /// <returns></returns>
        /// <param name='sb'></param>
        /// <param name='sop'</param>
        /// <param name='showFull'>
        /// If true then information on each inventory item will be shown.
        /// If false then only summary inventory information is shown.
        /// </param>
        private StringBuilder AddScenePartReport(StringBuilder sb, SceneObjectPart sop, bool showFull)
        {
            ConsoleDisplayList cdl = new ConsoleDisplayList();
            cdl.AddRow("Name", sop.Name);
            cdl.AddRow("Description", sop.Description);
            cdl.AddRow("Local ID", sop.LocalId);
            cdl.AddRow("UUID", sop.UUID);
            cdl.AddRow("Location",  string.Format("{0} @ {1}", sop.AbsolutePosition, sop.ParentGroup.Scene.Name));
            cdl.AddRow(
                "Parent",
                sop.IsRoot ? "Is Root" : string.Format("{0} {1}", sop.ParentGroup.Name, sop.ParentGroup.UUID));
            cdl.AddRow("Link number", sop.LinkNum);
            cdl.AddRow("Flags", sop.Flags);

            object itemsOutput;
            if (showFull)
            {
                StringBuilder itemsSb = new StringBuilder("\n");
                itemsOutput = AddScenePartItemsReport(itemsSb, sop.Inventory).ToString();
            }
            else
            {
                itemsOutput = sop.Inventory.Count;
            }


            cdl.AddRow("Items", itemsOutput);

            return sb.Append(cdl.ToString());
        }

        private StringBuilder AddScenePartItemsReport(StringBuilder sb, IEntityInventory inv)
        {
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.Indent = 2;

            cdt.AddColumn("Name", 50);
            cdt.AddColumn("Type", 12);
            cdt.AddColumn("Running", 7);
            cdt.AddColumn("Item UUID", 36);
            cdt.AddColumn("Asset UUID", 36);

            foreach (TaskInventoryItem item in inv.GetInventoryItems())
                cdt.AddRow(
                    item.Name,
                    ((InventoryType)item.InvType).ToString(),
                    (InventoryType)item.InvType == InventoryType.LSL ? item.ScriptRunning.ToString() : "n/a",
                    item.ItemID.ToString(),
                    item.AssetID.ToString());

            return sb.Append(cdt.ToString());
        }

        private void HandleDeleteObject(string module, string[] cmd)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmd.Length < 3)
                return;

            string mode = cmd[2];
            string o = "";

            if (mode != "outside")
            {
                if (cmd.Length < 4)
                    return;

                o = cmd[3];
            }

            List<SceneObjectGroup> deletes = null;
            UUID match;
            bool requireConfirmation = true;

            switch (mode)
            {
                case "owner":
                    if (!UUID.TryParse(o, out match))
                        return;

                    deletes = new List<SceneObjectGroup>();

                    m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                    {
                        if (g.OwnerID == match && !g.IsAttachment)
                            deletes.Add(g);
                    });
        
        //                if (deletes.Count == 0)
        //                    m_console.OutputFormat("No objects were found with owner {0}", match);
        
                    break;
        
                case "creator":
                    if (!UUID.TryParse(o, out match))
                        return;

                    deletes = new List<SceneObjectGroup>();

                    m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                    {
                        if (g.RootPart.CreatorID == match && !g.IsAttachment)
                            deletes.Add(g);
                    });
        
        //                if (deletes.Count == 0)
        //                    m_console.OutputFormat("No objects were found with creator {0}", match);
        
                    break;
        
                case "uuid":
                    if (!UUID.TryParse(o, out match))
                        return;

                    requireConfirmation = false;
                    deletes = new List<SceneObjectGroup>();
        
                    m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                    {
                        if (g.UUID == match && !g.IsAttachment)
                            deletes.Add(g);
                    });
        
        //                if (deletes.Count == 0)
        //                    m_console.OutputFormat("No objects were found with uuid {0}", match);
        
                    break;
        
                case "name":
                    deletes = GetDeleteCandidatesByName(module, cmd);
                    break;
        
                case "outside":
                    deletes = new List<SceneObjectGroup>();

                    m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                    {
                        SceneObjectPart rootPart = g.RootPart;
                        bool delete = false;
        
                        if (rootPart.GroupPosition.Z < 0.0 || rootPart.GroupPosition.Z > 10000.0)
                        {
                            delete = true;
                        }
                        else
                        {
                            ILandObject parcel
                                = m_scene.LandChannel.GetLandObject(rootPart.GroupPosition.X, rootPart.GroupPosition.Y);
        
                            if (parcel == null || parcel.LandData.Name == "NO LAND")
                                delete = true;
                        }
        
                        if (delete && !g.IsAttachment && !deletes.Contains(g))
                            deletes.Add(g);
                    });
        
                    if (deletes.Count == 0)
                        m_console.OutputFormat("No objects were found outside region bounds");
        
                    break;

                case "pos":
                    deletes = GetDeleteCandidatesByPos(module, cmd);
                    break;

                default:
                    m_console.OutputFormat("Unrecognized mode {0}", mode);
                    return;
            }

            if (deletes == null || deletes.Count <= 0)
                return;

            if (requireConfirmation)
            {
                string response = MainConsole.Instance.CmdPrompt(
                    string.Format(
                        "Are you sure that you want to delete {0} objects from {1}",
                        deletes.Count, m_scene.RegionInfo.RegionName),
                    "y/N");
    
                if (response.ToLower() != "y")
                {
                    MainConsole.Instance.OutputFormat(
                        "Aborting delete of {0} objects from {1}", deletes.Count, m_scene.RegionInfo.RegionName);

                    return;
                }
            }

            m_console.OutputFormat("Deleting {0} objects in {1}", deletes.Count, m_scene.RegionInfo.RegionName);

            foreach (SceneObjectGroup g in deletes)
            {
                m_console.OutputFormat("Deleting object {0} {1}", g.UUID, g.Name);
                m_scene.DeleteSceneObject(g, false);
            }
        }

        private List<SceneObjectGroup> GetDeleteCandidatesByName(string module, string[] cmdparams)
        {
            bool useRegex = false;
            OptionSet options = new OptionSet().Add("regex", v=> useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: delete object name [--regex] <name>");
                return null;
            }

            string name = mainParams[3];

            List<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();
            Action<SceneObjectGroup> searchAction;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchAction = so => { if (nameRegex.IsMatch(so.Name)) { sceneObjects.Add(so); }};
            }
            else
            {
                searchAction = so => { if (so.Name == name) { sceneObjects.Add(so); }};
            }

            m_scene.ForEachSOG(searchAction);

            if (sceneObjects.Count == 0)
                m_console.OutputFormat("No objects with name {0} found in {1}", name, m_scene.RegionInfo.RegionName);

            return sceneObjects;
        }

        /// <summary>
        /// Get scene object delete candidates by position
        /// </summary>
        /// <param name='module'></param>
        /// <param name='cmdparams'></param>
        /// <returns>null if parsing failed on one of the arguments, otherwise a list of objects to delete.  If there
        /// are no objects to delete then the list will be empty./returns>
        private List<SceneObjectGroup> GetDeleteCandidatesByPos(string module, string[] cmdparams)
        {
            if (cmdparams.Length < 5)
            {
                m_console.OutputFormat("Usage: delete object pos <start-coord> to <end-coord>");
                return null;
            }

            Vector3 startVector, endVector;

            if (!TryParseVectorRange(cmdparams.Skip(3).Take(3), out startVector, out endVector))
                return null;

            return m_scene.GetSceneObjectGroups().FindAll(
                so => !so.IsAttachment && Util.IsInsideBox(so.AbsolutePosition, startVector, endVector));
        }

        private bool TryParseVectorRange(IEnumerable<string> rawComponents, out Vector3 startVector, out Vector3 endVector)
        {
            string rawConsoleStartVector = rawComponents.Take(1).Single();

            if (!ConsoleUtil.TryParseConsoleMinVector(rawConsoleStartVector, out startVector))
            {
                m_console.OutputFormat("Error: Start vector {0} does not have a valid format", rawConsoleStartVector);
                endVector = Vector3.Zero;
                
                return false;
            }

            string rawConsoleEndVector = rawComponents.Skip(1).Take(1).Single();

            if (!ConsoleUtil.TryParseConsoleMaxVector(rawConsoleEndVector, out endVector))
            {
                m_console.OutputFormat("Error: End vector {0} does not have a valid format", rawConsoleEndVector);
                return false;
            }

            return true;
        }
    }
}