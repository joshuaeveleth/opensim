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
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.Avatar.Chat;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.CoreModules.Avatar.Chat.Tests
{
    [TestFixture]
    public class ChatModuleTests : OpenSimTestCase
    {  
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            // We must do this here so that child agent positions are updated in a predictable manner.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        /// <summary>
        /// Tests chat between neighbour regions on the east-west axis
        /// </summary>
        /// <remarks>
        /// Really, this is a combination of a child agent position update test and a chat range test.  These need
        /// to be separated later on.
        /// </remarks>
        [Test]
        public void TestInterRegionChatDistanceEastWest()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID sp1Uuid = TestHelpers.ParseTail(0x11);
            UUID sp2Uuid = TestHelpers.ParseTail(0x12);

            Vector3 sp1Position = new Vector3(6, 128, 20);
            Vector3 sp2Position = new Vector3(250, 128, 20);

            // XXX: HTTP server is not (and should not be) necessary for this test, though it's absence makes the 
            // CapabilitiesModule complain when it can't set up HTTP endpoints.
//            BaseHttpServer httpServer = new BaseHttpServer(99999);
//            MainServer.AddHttpServer(httpServer);
//            MainServer.Instance = httpServer;

            // We need entity transfer modules so that when sp2 logs into the east region, the region calls 
            // EntityTransferModuleto set up a child agent on the west region.
            // XXX: However, this is not an entity transfer so is misleading.
            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Chat");
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);

            SceneHelpers sh = new SceneHelpers();

            TestScene sceneWest = sh.SetupScene("sceneWest", TestHelpers.ParseTail(0x1), 1000, 1000);            
            TestScene sceneEast = sh.SetupScene("sceneEast", TestHelpers.ParseTail(0x2), 1001, 1000);            
            SceneHelpers.SetupSceneModules(new Scene[] { sceneWest, sceneEast }, config, lscm);

            SceneHelpers.SetupSceneModules(sceneWest, config, new CapabilitiesModule(), etmA, new ChatModule());           
            SceneHelpers.SetupSceneModules(sceneEast, config, new CapabilitiesModule(), etmB, new ChatModule());

            ScenePresence sp1 = SceneHelpers.AddScenePresence(sceneEast, sp1Uuid);
            TestClient sp1Client = (TestClient)sp1.ControllingClient;

            // If we don't set agents to flying, test will go wrong as they instantly fall to z = 0.
            // TODO: May need to create special complete no-op test physics module rather than basic physics, since
            // physics is irrelevant to this test.
            sp1.Flying = true;
            sp1.AbsolutePosition = sp1Position;                

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(sp2Uuid);
            TestClient tc = new TestClient(acd, sceneEast);
            List<TestClient> destinationTestClients = new List<TestClient>();
            EntityTransferHelpers.SetupInformClientOfNeighbourTriggersNeighbourClientCreate(tc, destinationTestClients);

            ScenePresence sp2 = SceneHelpers.AddScenePresence(sceneWest, tc, acd);
            TestClient sp2Client = (TestClient)sp2.ControllingClient;

            sp2.Flying = true;
            sp2.AbsolutePosition = sp2Position;

            TestClient sp2ChildClient = destinationTestClients[0];

            // We must update the scene in order to make the new root agent sp2 in sceneWest trigger a position update to its
            // child in sceneEast.
            sceneWest.Update(1);

            // Check child position is correct.
            Assert.AreEqual(
                new Vector3(sp2Position.X - sceneWest.RegionInfo.RegionSizeX, sp2Position.Y, sp2Position.Z), 
                sp2ChildClient.SceneAgent.AbsolutePosition);

            // Check chat received
            string receivedChatMessage = "";

            sp2ChildClient.OnReceivedChatMessage 
                += (message, type, fromPos, fromName, fromAgentID, ownerID, source, audible) => receivedChatMessage = message;

            string testMessage = "'ello darling";
            sp1Client.Chat(0, ChatTypeEnum.Say, testMessage);
                      
            Assert.AreEqual(testMessage, receivedChatMessage);
        }
    }
}