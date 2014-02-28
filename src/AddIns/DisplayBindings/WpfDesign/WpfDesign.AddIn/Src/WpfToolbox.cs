﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Linq;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Widgets.SideBar;
using WPF = System.Windows.Controls;

namespace ICSharpCode.WpfDesign.AddIn
{
	/// <summary>
	/// Manages the WpfToolbox.
	/// </summary>
	public class WpfToolbox
	{
		static WpfToolbox instance;
		
		public static WpfToolbox Instance {
			get {
				WorkbenchSingleton.AssertMainThread();
				if (instance == null) {
					instance = new WpfToolbox();
				}
				return instance;
			}
		}
		
		IToolService toolService;
		WpfSideBar sideBar;
		
		public WpfToolbox()
		{
			sideBar = new WpfSideBar();
			SideTab sideTab = new SideTab(sideBar, "Windows Presentation Foundation");
			sideTab.DisplayName = StringParser.Parse(sideTab.Name);
			sideTab.CanBeDeleted = false;
			sideTab.ChoosedItemChanged += OnChoosedItemChanged;
			
			sideTab.Items.Add(new WpfSideTabItem());
			foreach (Type t in Metadata.GetPopularControls())
				sideTab.Items.Add(new WpfSideTabItem(t));
			
			sideBar.Tabs.Add(sideTab);
			sideBar.ActiveTab = sideTab;
		}
		
		static bool IsControl(Type t)
		{
			return !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsSubclassOf(typeof(FrameworkElement));
		}


		private static HashSet<string> addedAssemblys = new HashSet<string>();
		public void AddProjectDlls(OpenedFile file)
		{
			var pc = MyTypeFinder.GetProjectContent(file);
			foreach (var referencedProjectContent in pc.ThreadSafeGetReferencedContents())
			{
				string f = null;
				if (referencedProjectContent is ParseProjectContent)
				{
					var prj = ((ParseProjectContent)referencedProjectContent).Project as AbstractProject;
					if (prj != null)
						f = prj.OutputAssemblyFullPath;
				}
				else if (referencedProjectContent is ReflectionProjectContent)
				{
					f = ((ReflectionProjectContent) referencedProjectContent).AssemblyLocation;
				}
				
				if (f != null && !addedAssemblys.Contains(f))
				{
					try
					{
						// DO NOT USE Assembly.LoadFrom!!!
						// see http://community.sharpdevelop.net/forums/t/19968.aspx
//						var assembly = Assembly.LoadFrom(f);
//
//						SideTab sideTab = new SideTab(sideBar, assembly.FullName.Split(new[] {','})[0]);
//						sideTab.DisplayName = StringParser.Parse(sideTab.Name);
//						sideTab.CanBeDeleted = false;
//						sideTab.ChoosedItemChanged += OnChoosedItemChanged;
//
//						sideTab.Items.Add(new WpfSideTabItem());
//
//						foreach (var t in assembly.GetExportedTypes())
//						{
//							if (IsControl(t))
//							{
//								sideTab.Items.Add(new WpfSideTabItem(t));
//							}
//						}
//
//						if (sideTab.Items.Count > 1)
//							sideBar.Tabs.Add(sideTab);
//
//						addedAssemblys.Add(f);
					}
					catch (Exception ex)
					{
						WpfViewContent.DllLoadErrors.Add(
							new Task(new BuildError(f, ex.Message)));
					}
				}
			}
		}

		void OnChoosedItemChanged(object sender, EventArgs e)
		{
			if (toolService != null) {
				ITool newTool = null;
				if (sideBar.ActiveTab != null && sideBar.ActiveTab.ChoosedItem != null) {
					newTool = sideBar.ActiveTab.ChoosedItem.Tag as ITool;
				}
				toolService.CurrentTool = newTool ?? toolService.PointerTool;
			}
		}
		
		public Control ToolboxControl {
			get { return sideBar; }
		}
		
		public IToolService ToolService {
			get { return toolService; }
			set {
				if (toolService != null) {
					toolService.CurrentToolChanged -= OnCurrentToolChanged;
				}
				toolService = value;
				if (toolService != null) {
					toolService.CurrentToolChanged += OnCurrentToolChanged;
					OnCurrentToolChanged(null, null);
				}
			}
		}
		
		void OnCurrentToolChanged(object sender, EventArgs e)
		{
			object tagToFind;
			if (toolService.CurrentTool == toolService.PointerTool) {
				tagToFind = null;
			} else {
				tagToFind = toolService.CurrentTool;
			}
			if (sideBar.ActiveTab.ChoosedItem != null) {
				if (sideBar.ActiveTab.ChoosedItem.Tag == tagToFind)
					return;
			}
			foreach (SideTabItem item in sideBar.ActiveTab.Items) {
				if (item.Tag == tagToFind) {
					sideBar.ActiveTab.ChoosedItem = item;
					sideBar.Refresh();
					return;
				}
			}
			foreach (SideTab tab in sideBar.Tabs) {
				foreach (SideTabItem item in tab.Items) {
					if (item.Tag == tagToFind) {
						sideBar.ActiveTab = tab;
						sideBar.ActiveTab.ChoosedItem = item;
						sideBar.Refresh();
						return;
					}
				}
			}
			sideBar.ActiveTab.ChoosedItem = null;
			sideBar.Refresh();
		}
		
		sealed class WpfSideBar : SharpDevelopSideBar
		{
			protected override object StartItemDrag(SideTabItem draggedItem)
			{
				if (this.ActiveTab.ChoosedItem != draggedItem && this.ActiveTab.Items.Contains(draggedItem)) {
					this.ActiveTab.ChoosedItem = draggedItem;
				}
				
				if (draggedItem.Tag != null) {
					return new System.Windows.DataObject(draggedItem.Tag);
				}
				else {
					return new System.Windows.DataObject();
				}
			}
		}
	}
}
