﻿using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Windows.Forms;
using ReClassNET.Extensions;
using ReClassNET.Nodes;
using ReClassNET.Project;

namespace ReClassNET.UI
{
	public partial class ClassNodeView : UserControl
	{
		/// <summary>A custom tree node for class nodes with hierarchical structure.</summary>
		private class ClassTreeNode : TreeNode
		{
			private readonly ClassNodeView control;

			public ClassNode ClassNode { get; }

			/// <summary>Constructor of the class.</summary>
			/// <param name="node">The class node.</param>
			/// <param name="control">The <see cref="ClassNodeView"/> instance this node should belong to.</param>
			public ClassTreeNode(ClassNode node, ClassNodeView control)
				: this(node, control, null)
			{
				Contract.Requires(node != null);
				Contract.Requires(control != null);
			}

			private ClassTreeNode(ClassNode node, ClassNodeView control, HashSet<ClassNode> seen)
			{
				Contract.Requires(node != null);
				Contract.Requires(control != null);

				ClassNode = node;

				this.control = control;

				Text = node.Name;

				ImageIndex = 1;
				SelectedImageIndex = 1;

				RebuildClassHierarchy(seen ?? new HashSet<ClassNode> { ClassNode });
			}

			public void Update()
			{
				Text = ClassNode.Name;

				RebuildClassHierarchy(new HashSet<ClassNode> { ClassNode });
			}

			/// <summary>Rebuilds the class hierarchy.</summary>
			/// <param name="seen">The already seen classes.</param>
			private void RebuildClassHierarchy(HashSet<ClassNode> seen)
			{
				Contract.Requires(seen != null);

				if (!control.EnableClassHierarchyView)
				{
					return;
				}

				var distinctClasses = ClassNode.Nodes
					.OfType<BaseWrapperNode>()
					.Select(w => w.ResolveMostInnerNode())
					.OfType<ClassNode>()
					.Distinct()
					.ToList();

				if (distinctClasses.SequenceEqualsEx(Nodes.Cast<ClassTreeNode>().Select(t => t.ClassNode)))
				{
					return;
				}

				Nodes.Clear();

				foreach (var child in distinctClasses)
				{
					var childSeen = new HashSet<ClassNode>(seen);
					if (childSeen.Add(child))
					{
						Nodes.Add(new ClassTreeNode(child, control, childSeen));
					}
				}

				if (control.AutoExpandClassNodes)
				{
					Expand();
				}
			}
		}

		public class EnumTreeNode : TreeNode
		{
			public EnumMetaData Enum { get; }

			public EnumTreeNode(EnumMetaData @enum)
			{
				Contract.Requires(@enum != null);

				Enum = @enum;

				ImageIndex = 3;
				SelectedImageIndex = 3;
			}

			public void Update()
			{
				Text = Enum.Name;
			}
		}

		private class NodeSorter : IComparer
		{
			public int Compare(object x, object y)
			{
				var compare = Application.CurrentCulture.CompareInfo;

				if (x is ClassTreeNode cn1 && y is ClassTreeNode cn2)
				{
					return compare.Compare(cn1.Text, cn2.Text);
				}
				if (x is EnumTreeNode en1 && y is EnumTreeNode en2)
				{
					return compare.Compare(en1.Text, en2.Text);
				}

				return 0;
			}
		}

		private readonly TreeNode enumsRootNode;
		private readonly TreeNode classesRootNode;

		private ClassNode selectedClass;

		private bool autoExpandClassNodes;
		private bool enableClassHierarchyView;

		public delegate void SelectionChangedEvent(object sender, ClassNode node);
		public event SelectionChangedEvent SelectionChanged;

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ClassNode SelectedClass
		{
			get => selectedClass;
			set
			{
				if (selectedClass != value)
				{
					selectedClass = value;
					if (selectedClass != null)
					{
						classesTreeView.SelectedNode = FindMainClassTreeNode(selectedClass);
					}

					SelectionChanged?.Invoke(this, selectedClass);
				}
			}
		}

		[DefaultValue(false)]
		public bool AutoExpandClassNodes
		{
			get => autoExpandClassNodes;
			set
			{
				if (autoExpandClassNodes != value)
				{
					autoExpandClassNodes = value;

					if (autoExpandClassNodes)
					{
						ExpandAllClassNodes();
					}
				}
			}
		}

		[DefaultValue(false)]
		public bool EnableClassHierarchyView
		{
			get => enableClassHierarchyView;
			set
			{
				if (enableClassHierarchyView != value)
				{
					enableClassHierarchyView = value;

					var classes = classesRootNode.Nodes.Cast<ClassTreeNode>().Select(t => t.ClassNode).ToList();

					classesRootNode.Nodes.Clear();

					AddClasses(classes);
				}
			}
		}

		public ContextMenuStrip ProjectTreeNodeContextMenuStrip { get; set; }

		public ContextMenuStrip ClassTreeNodeContextMenuStrip { get; set; }

		public ClassNodeView()
		{
			Contract.Ensures(classesRootNode != null);

			InitializeComponent();

			DoubleBuffered = true;

			classesTreeView.TreeViewNodeSorter = new NodeSorter();
			classesTreeView.ImageList = new ImageList();
			classesTreeView.ImageList.Images.Add(Properties.Resources.B16x16_Text_List_Bullets);
			classesTreeView.ImageList.Images.Add(Properties.Resources.B16x16_Class_Type);
			classesTreeView.ImageList.Images.Add(Properties.Resources.B16x16_Category);
			classesTreeView.ImageList.Images.Add(Properties.Resources.B16x16_Enum_Type);

			classesRootNode = new TreeNode
			{
				Text = "Classes",
				ImageIndex = 0,
				SelectedImageIndex = 0
			};

			classesTreeView.Nodes.Add(classesRootNode);

			enumsRootNode = new TreeNode
			{
				Text = "Enums",
				ImageIndex = 2,
				SelectedImageIndex = 2
			};

			classesTreeView.Nodes.Add(enumsRootNode);
		}

		#region Event Handler

		private void classesTreeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Level == 0)
			{
				return;
			}

			if (!(e.Node is ClassTreeNode node))
			{
				return;
			}

			if (selectedClass != node.ClassNode)
			{
				SelectedClass = node.ClassNode;
			}
		}

		private void classesTreeView_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right)
			{
				return;
			}

			var node = classesTreeView.GetNodeAt(e.X, e.Y);
			if (node == null)
			{
				return;
			}

			if (node is ClassTreeNode)
			{
				classesTreeView.SelectedNode = node;

				var cms = ClassTreeNodeContextMenuStrip;
				cms?.Show(classesTreeView, e.Location);
			}
			else if (node == classesRootNode)
			{
				var cms = ProjectTreeNodeContextMenuStrip;
				cms?.Show(classesTreeView, e.Location);
			}
		}

		private void classesTreeView_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			var isClassTreeNode = e.Node is ClassTreeNode;
			e.CancelEdit = !isClassTreeNode;
		}

		private void classesTreeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.Label))
			{
				if (e.Node is ClassTreeNode node)
				{
					node.ClassNode.Name = e.Label;

					// Cancel the edit if the class refused the name.
					// This prevents the tree node from using the wrong name.
					if (node.ClassNode.Name != e.Label)
					{
						e.CancelEdit = true;
					}
				}
			}
		}

		#endregion

		public void ExpandAllClassNodes()
		{
			classesRootNode.ExpandAll();
		}

		public void CollapseAllClassNodes()
		{
			foreach (var tn in classesRootNode.Nodes.Cast<TreeNode>())
			{
				tn.Collapse();
			}
		}

		public void Clear()
		{
			classesRootNode.Nodes.Clear();
			enumsRootNode.Nodes.Clear();
		}

		/// <summary>Adds the class to the view.</summary>
		/// <param name="node">The class to add.</param>
		public void AddClass(ClassNode node)
		{
			Contract.Requires(node != null);

			AddClasses(new[] { node });
		}

		public void AddClasses(IEnumerable<ClassNode> nodes)
		{
			Contract.Requires(nodes != null);

			classesTreeView.BeginUpdate();

			foreach (var node in nodes)
			{
				classesRootNode.Nodes.Add(new ClassTreeNode(node, this));
			}

			classesRootNode.Expand();

			classesTreeView.Sort();

			classesTreeView.EndUpdate();
		}

		/// <summary>Removes the class from the view.</summary>
		/// <param name="node">The class to remove.</param>
		public void RemoveClass(ClassNode node)
		{
			Contract.Requires(node != null);

			foreach (var tn in FindClassTreeNodes(node))
			{
				tn.Remove();
			}

			if (selectedClass == node)
			{
				if (classesRootNode.Nodes.Count > 0)
				{
					classesTreeView.SelectedNode = classesRootNode.Nodes[0];
				}
				else
				{
					SelectedClass = null;
				}
			}
		}

		/// <summary>Searches for the <see cref="ClassTreeNode"/> which represents the class.</summary>
		/// <param name="node">The class to search.</param>
		/// <returns>The found class tree node.</returns>
		private ClassTreeNode FindMainClassTreeNode(ClassNode node)
		{
			Contract.Requires(node != null);

			return classesRootNode.Nodes
				.Cast<ClassTreeNode>()
				.FirstOrDefault(t => t.ClassNode == node);
		}

		/// <summary>Searches for the ClassTreeNode which represents the class.</summary>
		/// <param name="node">The class to search.</param>
		/// <returns>The found class tree node.</returns>
		private IEnumerable<ClassTreeNode> FindClassTreeNodes(ClassNode node)
		{
			Contract.Requires(node != null);

			return classesRootNode.Nodes
				.Cast<ClassTreeNode>()
				.Traverse(t => t.Nodes.Cast<ClassTreeNode>())
				.Where(n => n.ClassNode == node);
		}

		public void UpdateClassNode(ClassNode node)
		{
			Contract.Requires(node != null);

			classesTreeView.BeginUpdate();

			foreach (var tn in FindClassTreeNodes(node))
			{
				tn.Update();
			}

			classesTreeView.Sort();

			classesTreeView.EndUpdate();
		}

		public void AddEnum(EnumMetaData @enum)
		{
			Contract.Requires(@enum != null);

			AddEnums(new[] { @enum });
		}

		public void AddEnums(IEnumerable<EnumMetaData> enums)
		{
			Contract.Requires(enums != null);

			classesTreeView.BeginUpdate();

			foreach (var @enum in enums)
			{
				enumsRootNode.Nodes.Add(new EnumTreeNode(@enum));
			}

			enumsRootNode.ExpandAll();

			classesTreeView.Sort();

			classesTreeView.EndUpdate();
		}

		public void UpdateEnumNode(EnumMetaData @enum)
		{
			Contract.Requires(@enum != null);

			classesTreeView.BeginUpdate();

			var nodes = enumsRootNode.Nodes
				.Cast<EnumTreeNode>()
				.Where(n => n.Enum == @enum);
			foreach (var tn in nodes)
			{
				tn.Update();
			}

			if (nodes.None())
			{
				AddEnum(@enum);
			}

			classesTreeView.Sort();

			classesTreeView.EndUpdate();
		}
	}
}
