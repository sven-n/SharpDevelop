﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

// enable this define to test that event handlers are removed correctly
//#define EventHandlerDebugging

using System;
using System.Diagnostics;
using System.Windows;
using ICSharpCode.WpfDesign.XamlDom;
using ICSharpCode.WpfDesign.Designer.Services;
using System.Windows.Markup;

namespace ICSharpCode.WpfDesign.Designer.Xaml
{
	[DebuggerDisplay("XamlDesignItem: {ComponentType.Name}")]
	sealed class XamlDesignItem : DesignItem
	{
		readonly XamlObject _xamlObject;
		readonly XamlDesignContext _designContext;
		readonly XamlModelPropertyCollection _properties;
		UIElement _view;
		
		public XamlDesignItem(XamlObject xamlObject, XamlDesignContext designContext)
		{
			this._xamlObject = xamlObject;
			this._designContext = designContext;
			this._properties = new XamlModelPropertyCollection(this);
		}
		
		internal XamlComponentService ComponentService {
			get {
				return _designContext._componentService;
			}
		}
		
		internal XamlObject XamlObject {
			get { return _xamlObject; }
		}
		
		public override object Component {
			get {
				return _xamlObject.Instance;
			}
		}
		
		public override Type ComponentType {
			get { return _xamlObject.ElementType; }
		}
		
		void SetNameInternal(string newName)
		{
			_xamlObject.Name = newName;
		}
		
		public override string Name {
			get { return _xamlObject.Name; }
			set {
				UndoService undoService = this.Services.GetService<UndoService>();
				if (undoService != null)
					undoService.Execute(new SetNameAction(this, value));
				else
					SetNameInternal(value);
			}
		}
		
		public override string Key {
			get { return XamlObject.GetXamlAttribute("Key"); }
			set { XamlObject.SetXamlAttribute("Key", value); }
		}
		
		#if EventHandlerDebugging
		static int totalEventHandlerCount;
		#endif
		
		/// <summary>
		/// Is raised when the name of the design item changes.
		/// </summary>
		public override event EventHandler NameChanged {
			add {
				#if EventHandlerDebugging
				Debug.WriteLine("Add event handler to " + this.ComponentType.Name + " (handler count=" + (++totalEventHandlerCount) + ")");
				#endif
				_xamlObject.NameChanged += value;
			}
			remove {
				#if EventHandlerDebugging
				Debug.WriteLine("Remove event handler from " + this.ComponentType.Name + " (handler count=" + (--totalEventHandlerCount) + ")");
				#endif
				_xamlObject.NameChanged -= value;
			}
		}
		
		public override DesignItem Parent {
			get {
				if (_xamlObject.ParentProperty == null)
					return null;
				else
					return ComponentService.GetDesignItem(_xamlObject.ParentProperty.ParentObject.Instance);
			}
		}
		
		public override DesignItemProperty ParentProperty {
			get {
				DesignItem parent = this.Parent;
				if (parent == null)
					return null;
				XamlProperty prop = _xamlObject.ParentProperty;
				if (prop.IsAttached) {
					return parent.Properties.GetAttachedProperty(prop.PropertyTargetType, prop.PropertyName);
				} else {
					return parent.Properties.GetProperty(prop.PropertyName);
				}
			}
		}
		
		/// <summary>
		/// Occurs when the parent of this design item changes.
		/// </summary>
		public override event EventHandler ParentChanged {
			add    { _xamlObject.ParentPropertyChanged += value; }
			remove { _xamlObject.ParentPropertyChanged += value; }
		}
		
		public override UIElement View {
			get {
				if (_view != null)
					return _view;
				else
					return this.Component as UIElement;
			}
		}
		
		internal void SetView(UIElement newView)
		{
			_view = newView;
		}
		
		public override DesignContext Context {
			get { return _designContext; }
		}
		
		public override DesignItemPropertyCollection Properties {
			get { return _properties; }
		}
		
		internal void NotifyPropertyChanged(XamlModelProperty property)
		{
			Debug.Assert(property != null);
			OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(property.Name));
		}

		public override string ContentPropertyName {
			get {
				return XamlObject.ContentPropertyName; 
			}
		}
		
		/// <summary>
		/// Item is Locked at Design Time
		/// </summary>
		public bool IsDesignTimeLocked { 
			get {
				var locked = Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).ValueOnInstance;
				return (locked != null && (bool) locked == true);
			}
			set {
				if (value)
					Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).SetValue(true);
				else
					Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).Reset();
			}
				
		}
		
		public override DesignItem Clone()
		{
			DesignItem item = null;
		    var xaml = XamlStaticTools.GetXaml(this.XamlObject);
		    XamlDesignItem rootItem = Context.RootItem as XamlDesignItem;
		    var obj = XamlParser.ParseSnippet(rootItem.XamlObject, xaml, ((XamlDesignContext) Context).ParserSettings);
		    if (obj != null)
		    {
                item = ((XamlDesignContext)Context)._componentService.RegisterXamlComponentRecursive(obj);
		    }
		    return item;
		}
		
		sealed class SetNameAction : ITransactionItem
		{
			XamlDesignItem designItem;
			string oldName;
			string newName;
			
			public SetNameAction(XamlDesignItem designItem, string newName)
			{
				this.designItem = designItem;
				this.newName = newName;
				
				oldName = designItem.Name;
			}
			
			public string Title {
				get {
					return "Set name";
				}
			}
			
			public void Do()
			{
				designItem.SetNameInternal(newName);
			}
			
			public void Undo()
			{
				designItem.SetNameInternal(oldName);
			}
			
			public System.Collections.Generic.ICollection<DesignItem> AffectedElements {
				get {
					return new DesignItem[] { designItem };
				}
			}

			public bool MergeWith(ITransactionItem other)
			{
				SetNameAction o = other as SetNameAction;
				if (o != null && designItem == o.designItem) {
					newName = o.newName;
					return true;
				}
				return false;
			}
		}
	}
}
