﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace GeneTree
{
	[Serializable]
	public class Tree
	{
		public TreeNode _root;

		[XmlIgnore]
		public List<TreeNode> _nodes = new List<TreeNode>();

		[XmlIgnore]
		public GeneticAlgorithmRunResults _prevResults;
		
		[XmlIgnore]
		public GeneticAlgorithmRunResults _currentResults;
		
		//TODO remove this for better tracking somewhere else
		public string _source;
		
		public bool _isDirty = true;
		
		public Tree Copy()
		{
			//create a new tree
			Tree new_tree = new Tree();
			new_tree._root = this._root.ReturnFullyLinkedCopyOfSelf();			
			new_tree.AddNodeWithChildren(new_tree._root);
			new_tree._prevResults = this._prevResults;
			
			return new_tree;			
		}
		
		public void RemoveZeroCountNodes()
		{
			//TODO this method needs to be unbroken
			
			return;
			Stack<TreeNode> nodes_to_process = new Stack<TreeNode>();
			nodes_to_process.Push(_root);
			
			while (nodes_to_process.Count > 0)
			{
				TreeNode node = nodes_to_process.Pop();
				
				if (node._traverseCount == 0)
				{
					//TODO change this to use a delete and new node, updating references along the way
					
					//node.ResetNodeToNoClass();
				}
				else
				{
					foreach (var subNode in node._subNodes)
					{
						nodes_to_process.Push(subNode);	
					} 
				}
			}
		}
			

		public void RemoveChildrenFromNode(TreeNode node)
		{
			foreach (var subNode in node._subNodes)
			{
				RemoveNodeWithChildren(subNode);
			}
		}

		public void RemoveNodeWithChildren(TreeNode node)
		{
			_nodes.Remove(node);
			
			RemoveChildrenFromNode(node);
		}
		
		public void AddRootToTree(TreeNode node)
		{
			this._root = node;
			
			AddNodeWithoutChildren(node);
		}

		public void AddNodeWithoutChildren(TreeNode node)
		{
			_nodes.Add(node);
			node._tree = this;
		}
		public void AddNodeWithChildren(TreeNode node)
		{
			AddNodeWithoutChildren(node);
			
			foreach (var subNode in node._subNodes)
			{
				AddNodeWithChildren(subNode);
			}
		}
		public bool TraverseData(DataPoint point, GeneticAlgorithmRunResults results)
		{
			return _root.TraverseData(point, results);
		}
		
		public void ProcessDataThroughTree(DataPointManager dataPointMgr, GeneticAlgorithmRunResults results)
		{
			//TODO this should return a full GeneticAlgorithmRunResults which can be processed
			
			//clear out the node counts
			this._nodes.ForEach(c => c._traverseCount = 0);
			
			ConfusionMatrix matrix = new ConfusionMatrix(dataPointMgr.classes.Length);
			foreach (var dataPoint in dataPointMgr._pointsToTest)
			{
				results.count_allData++;
				TraverseData(dataPoint, results);
			}

			//store the results for future use
			_currentResults = results;			
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			Stack<Tuple<TreeNode, int>> nodes = new Stack<Tuple<TreeNode, int>>();
			nodes.Push(new Tuple<TreeNode, int>(_root, 0));
			
			while (nodes.Count > 0)
			{
				var item = nodes.Pop();
				var node = item.Item1;
				
				sb.AppendLine();
				
				for (int i = 0; i < item.Item2; i++)
				{
					sb.Append("  ");
				}
				sb.Append(node.ToString());

				foreach (var subNode in node._subNodes)
				{
					nodes.Push(new Tuple<TreeNode, int>(subNode, item.Item2 + 1));
				}
			}
			return sb.ToString();
		}
		//TODO consider removing the Hash and Equals, not sure there are any duplicates
		public override int GetHashCode()
		{
			int hashCode = 0;
			unchecked
			{
				if (_root != null)
					hashCode += 1000000007 * _root.GetHashCode();
				if (_nodes != null)
					hashCode += 1000000009 * _nodes.GetHashCode();
				hashCode += 1000000021 * _prevResults.GetHashCode();
			}
			return hashCode;
		}

		public override bool Equals(object obj)
		{
			Tree other = obj as Tree;
			if (other == null)
				return false;
			return this.ToString() == other.ToString();
		}
		
		public void WriteToXmlFile(string filePath)
		{
			TextWriter writer = null;
			try
			{
				var serializer = new XmlSerializer(this.GetType());
				writer = new StreamWriter(filePath);
				serializer.Serialize(writer, this);
			}
			finally
			{
				if (writer != null)
				{
					writer.Close();
				}
			}
		}
		/// <summary>
		/// Reads a Tree in from an XML file.  Has to process the nodes and hierarchy after the fact to avoid circular references.
		/// </summary>
		/// <param name="filePath">location of XML file</param>
		/// <returns>a fully assembled Tree</returns>
		public static Tree ReadFromXmlFile(string filePath)
		{
			TextReader reader = null;
			Tree tree_read = null;
			try
			{
				var serializer = new XmlSerializer(typeof(Tree));
				reader = new StreamReader(filePath);
				tree_read = (Tree)serializer.Deserialize(reader);
				
				if (tree_read != null)
				{
					var nodes_to_process = new Stack<Tuple<TreeNode, TreeNode>>();
					nodes_to_process.Push(Tuple.Create(tree_read._root, (TreeNode)null));
				
					while (nodes_to_process.Count > 0)
					{
						var node_parent = nodes_to_process.Pop();
					
						var node = node_parent.Item1;
						tree_read._nodes.Add(node);
						node._parent = node_parent.Item2;					
						node._tree = tree_read;
					
						foreach (var subNode in node._subNodes)
						{
							nodes_to_process.Push(Tuple.Create(subNode, node));
						}
					}
				}
				else
				{
					throw new Exception("something went wrong reading the tree back");
				}
			
				return tree_read;
			}
			finally
			{
				if (reader != null)
					reader.Close();
			}
		}
	}
	
}
