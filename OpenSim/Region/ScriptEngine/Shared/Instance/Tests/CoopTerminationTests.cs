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
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Scripting.WorldComm;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.XEngine;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ScriptEngine.Shared.Instance.Tests
{
    /// <summary>
    /// Test that co-operative script thread termination is working correctly.
    /// </summary>
    [TestFixture]
    public class CoopTerminationTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private OpenSim.Region.ScriptEngine.XEngine.XEngine m_xEngine;

        private AutoResetEvent m_chatEvent;
        private AutoResetEvent m_stoppedEvent;

        private OSChatMessage m_osChatMessageReceived;

        [SetUp]
        public void Init()
        {
            m_osChatMessageReceived = null;
            m_chatEvent = new AutoResetEvent(false);
            m_stoppedEvent = new AutoResetEvent(false);

            //AppDomain.CurrentDomain.SetData("APPBASE", Environment.CurrentDirectory + "/bin");
//            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            m_xEngine = new OpenSim.Region.ScriptEngine.XEngine.XEngine();

            IniConfigSource configSource = new IniConfigSource();
            
            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            xEngineConfig.Set("ScriptStopStrategy", "co-op");

            // Make sure loops aren't actually being terminated by a script delay wait.
            xEngineConfig.Set("ScriptDelayFactor", 0);

            // This is really just set for debugging the test.
            xEngineConfig.Set("WriteScriptSourceToDebugFile", true);

            // Set to false if we need to debug test so the old scripts don't get wiped before each separate test
//            xEngineConfig.Set("DeleteScriptsOnStartup", false);

            // This is not currently used at all for co-op termination.  Bumping up to demonstrate that co-op termination
            // has an effect - without it tests will fail due to a 120 second wait for the event to finish.
            xEngineConfig.Set("WaitForEventCompletionOnScriptStop", 120000);

            m_scene = new SceneHelpers().SetupScene("My Test", TestHelpers.ParseTail(0x9999), 1000, 1000, configSource);
            SceneHelpers.SetupSceneModules(m_scene, configSource, m_xEngine);
            m_scene.StartScripts();
        }

        /// <summary>
        /// Test co-operative termination on derez of an object containing a script with a long-running event.
        /// </summary>
        /// <remarks>
        /// TODO: Actually compiling the script is incidental to this test.  Really want a way to compile test scripts
        /// within the build itself.
        /// </remarks>
        [Test]
        public void TestStopOnLongSleep()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        llSay(0, ""Thin Lizzy"");
        llSleep(60);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongSingleStatementForLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");
        
        for (i = 0; i < 2147483647; i++)        
            llSay(0, ""Iter "" + (string)i);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongCompoundStatementForLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");
        
        for (i = 0; i < 2147483647; i++) 
        {
            llSay(0, ""Iter "" + (string)i);
        }
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongSingleStatementWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        while (1 == 1)        
            llSay(0, ""Iter "" + (string)i++);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongCompoundStatementWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        while (1 == 1) 
        {
            llSay(0, ""Iter "" + (string)i++);
        }
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnLongDoWhileLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        do 
        {
            llSay(0, ""Iter "" + (string)i++);
} while (1 == 1);
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnInfiniteJumpLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        @p1;      
        llSay(0, ""Iter "" + (string)i++);
        jump p1;
    }
}";

            TestStop(script);
        }

        [Test]
        public void TestStopOnInfiniteUserFunctionCallLoop()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string script = 
@"
integer i = 0;

ufn1()
{
  llSay(0, ""Iter ufn1() "" + (string)i++);
  ufn1();
}

default
{    
    state_entry()
    {
        integer i = 0;
        llSay(0, ""Thin Lizzy"");

        ufn1();
    }
}";

            TestStop(script);
        }

        private void TestStop(string script)
        {
            UUID userId = TestHelpers.ParseTail(0x1);
//            UUID objectId = TestHelpers.ParseTail(0x100);
//            UUID itemId = TestHelpers.ParseTail(0x3);
            string itemName = "TestStop() Item";

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, userId, "TestStop", 0x100);
            m_scene.AddNewSceneObject(so, true);

            InventoryItemBase itemTemplate = new InventoryItemBase();
//            itemTemplate.ID = itemId;
            itemTemplate.Name = itemName;
            itemTemplate.Folder = so.UUID;
            itemTemplate.InvType = (int)InventoryType.LSL;

            m_scene.EventManager.OnChatFromWorld += OnChatFromWorld;

            SceneObjectPart partWhereRezzed = m_scene.RezNewScript(userId, itemTemplate, script);

            TaskInventoryItem rezzedItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);

            // Wait for the script to start the event before we try stopping it.
            m_chatEvent.WaitOne(60000);

            Console.WriteLine("Script started with message [{0}]", m_osChatMessageReceived.Message);

            // FIXME: This is a very poor way of trying to avoid a low-probability race condition where the script
            // executes llSay() but has not started the next statement before we try to stop it.
            Thread.Sleep(1000);

            // We need a way of carrying on if StopScript() fail, since it won't return if the script isn't actually
            // stopped.  This kind of multi-threading is far from ideal in a regression test.
            new Thread(() => { m_xEngine.StopScript(rezzedItem.ItemID); m_stoppedEvent.Set(); }).Start();

            if (!m_stoppedEvent.WaitOne(30000))
                Assert.Fail("Script did not co-operatively stop.");

            bool running;
            TaskInventoryItem scriptItem = partWhereRezzed.Inventory.GetInventoryItem(itemName);
            Assert.That(
                SceneObjectPartInventory.TryGetScriptInstanceRunning(m_scene, scriptItem, out running), Is.True);
            Assert.That(running, Is.False);
        }

        private void OnChatFromWorld(object sender, OSChatMessage oscm)
        {
            m_scene.EventManager.OnChatFromWorld -= OnChatFromWorld;
            Console.WriteLine("Got chat [{0}]", oscm.Message);

            m_osChatMessageReceived = oscm;
            m_chatEvent.Set();
        }
    }
}