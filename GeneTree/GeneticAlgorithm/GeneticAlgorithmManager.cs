﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MoreLinq;

namespace GeneTree
{
	public class GeneticAlgorithmManager
	{
		public GeneticAlgorithmOptions _gaOptions = new GeneticAlgorithmOptions();
		
		//TODO allow this to take a seed for reproducibiltiy later
		public Random rando = new Random();
		public DataPointManager dataPointMgr = new DataPointManager();
		
		public event EventHandler<EventArg<GeneticAlgorithmUpdateStatus>> ProgressUpdated;
		
		public void OnProgressUpdated(int progress)
		{
			var update = new GeneticAlgorithmUpdateStatus();
			update.progress = progress;
			update.status = Logger.GetStringAndFlush();
			
			var handler = ProgressUpdated;
			if (handler != null)
			{
				handler(this, new EventArg<GeneticAlgorithmUpdateStatus>(update));
			}
		}
		
		private List<Tree> CreateRandomPoolOfTrees(int size)
		{
			var trees = new List<Tree>();
			for (int i = 0; i < size; i++)
			{
				trees.Add(CreateRandomTree());
			}
			return trees;
		}

		public void CreatePoolOfGoodTrees()
		{
			List<Tree> theBest = new List<Tree>();
			
			//TODO create this dir once it's needed
			var new_dir = Directory.CreateDirectory("tree outputs\\" + DateTime.Now.Ticks);
			
			dataPointMgr.UpdateSubsetOfDatapoints(_gaOptions.prob_to_keep_data, rando);
			
			//HACK: data update only at start to ensure that trees are improving on same data
			_gaOptions.populationSize = _gaOptions.seq_inner_population;
			
			_gaOptions.generations = _gaOptions.seq_inner_generations;
			for (int run = 0; run < _gaOptions.seq_inner_run; run++)
			{
				//TODO use better logic to determine when to switch to new data
				if (run % 5 == 0)
				{
					dataPointMgr.UpdateSubsetOfDatapoints(_gaOptions.prob_to_keep_data, rando);
				}
				Logger.WriteLine("starting run at run {0}", run);
					
				var trees = ProcessTheNextGeneration();
				trees[0].WriteToXmlFile(Path.Combine(new_dir.FullName, string.Format("{0}.xml", run)));
			}
				
		}
		
		private List<Tree> ProcessPoolOfTrees(IEnumerable<Tree> trees, int generation)
		{
			List<Tree> keeps = new List<Tree>();
			
			Logger.WriteLine("point count: " + dataPointMgr._pointsToTest.Count);
			
			foreach (var tree in trees)
			{
				GeneticAlgorithmRunResults results = null;
				
				//quick pass through for trees already processed
				if (!tree._isDirty)
				{
					tree._source = "pass through";
					keeps.Add(tree);
					continue;
				}
				
				bool processAgain = true;
				while (processAgain)
				{
					results = new GeneticAlgorithmRunResults(this);
					processAgain = false;
					tree.ProcessDataThroughTree(dataPointMgr, results);
					
					break;
					
					foreach (var node in tree.GetNodesOfType<ClassificationTreeNode>())
					{
						//TODO subclass the empty node to avoid these sorts of issues
						if (node.Classification != -1)
						{
							var best_class = node.matrix.GetRowWithMaxCount();
							if (node.Classification != best_class)
							{
								node.Classification = best_class;
								processAgain = true;
							}
						}
					}
				}
				
				//moving this out of selectivity since node count affects score
				tree.RemoveZeroCountNodes();
				
				//TODO improve override for non improving score
				//will add kepeers if there was no previous result or if the score improved or randomly
				if (tree._prevResults == null ||
				    results.GetMetricResult > tree._prevResults.GetMetricResult ||
				    rando.NextDouble() > 0.95)
				{
					//only add the new tree to the results if the score improved
					keeps.Add(tree);
				}
				
				tree._isDirty = false;
				tree._prevResults = results;
			}
			
			return keeps;
		}
		
		public List<Tree> ProcessTheNextGeneration()
		{
			var starter = CreateRandomPoolOfTrees(_gaOptions.populationSize * 5);
			return ProcessTheNextGeneration(starter);
		}

		public List<Tree> ProcessTheNextGeneration(List<Tree> treesInPopulation)
		{
			//add a bunch of random columns for testing (1:1) for now
			int count_generated_features = dataPointMgr._columns.Count * 0;
			
			for (int i = 0; i < count_generated_features; i++)
			{
				dataPointMgr._columns.Add(GeneratedDataColumn.CreateNewRandom(this));
			}
			
			//TODO add a step to check for "convergence" and stop iterating
			for (int generationNumber = 0; generationNumber < _gaOptions.generations; generationNumber++)
			{
				Logger.WriteLine("generation: " + generationNumber);
				
				treesInPopulation = ProcessPoolOfTrees(treesInPopulation, generationNumber);
				
				foreach (var element in treesInPopulation.GroupBy(c=>c._source))
				{
					Logger.WriteLine(string.Format("{0} has {1} = {2}", 
							element.Key, element.Count(), 
							1.0 * element.Count() / _gaOptions.populationSize));
				}
				
				List<Tree> newCreations = new List<Tree>();
								
				//thin down the herd and take pop size or total
				treesInPopulation = treesInPopulation.Distinct()
					.OrderByDescending(c => c._prevResults.GetMetricResult)
					.Take((int)(_gaOptions.populationSize * _gaOptions.prob_population_to_keep))
					.ToList();
				
				//output some info on best
				//Logger.WriteLine(string.Join("\r\n", starter.Take(10).Select(c => c._prevResults.ToString())));
				Logger.WriteLine("");
				foreach (var tree in treesInPopulation.Take(10))
				{
					Logger.WriteLine("{0:0.000}", tree._prevResults.GetMetricResult);
				}
				
				foreach (var tree in treesInPopulation.Take(1))
				{
					Logger.WriteLine("");
					Logger.WriteLine(tree);
					Logger.WriteLine(tree._prevResults);
					
					//output the nodes matrices
					//TODO remove this break to get output again
					break;
					foreach (var node in tree._nodes)
					{
						if (node._traverseCount > 0)
						{
							Logger.WriteLine(node);
							Logger.WriteLine(node.matrix);
						}
					}
				}
				
				//this allows probs to change after each generation if desired
				var operations = new List<Tuple<GeneticOperations.GeneticOperation, double>>();
					
				operations.Add(Tuple.Create((GeneticOperations.GeneticOperation)GeneticOperations.SwapNodesBetweenTrees, _gaOptions.Prob_ops_swap));
				operations.Add(Tuple.Create((GeneticOperations.GeneticOperation)GeneticOperations.SplitNodeWithMostPopularClasses, _gaOptions.prob_node_split));
				operations.Add(Tuple.Create((GeneticOperations.GeneticOperation)GeneticOperations.ChangeValueForNode, _gaOptions.prob_ops_change));
				operations.Add(Tuple.Create((GeneticOperations.GeneticOperation)GeneticOperations.CreateRandomTree, _gaOptions.Prob_ops_new_tree));
				
				//TODO reinstate the delete node option (force to NO CLASS)
				//operations.Add(Tuple.Create((GeneticOperations.GeneticOperation)GeneticOperations.DeleteNodeFromTree, _gaOptions.prob_ops_delete));
					
				var operation_picker = new WeightedSelector<GeneticOperations.GeneticOperation>(operations);
				
				
				for (int populationNumber = 0; populationNumber < _gaOptions.populationSize; populationNumber++)
				{
					var operation = operation_picker.PickRandom(rando);
					newCreations.AddRange(operation(this, treesInPopulation));
				}
				
				//moves things to the master list
				treesInPopulation.AddRange(newCreations);
				
				OnProgressUpdated(100 * generationNumber / _gaOptions.generations);
			}

			OnProgressUpdated(100);
			
			return treesInPopulation;
		}

		public void LoadDataFile(string csv_path, string config_path)
		{
			dataPointMgr.SetConfiguration(config_path);
			dataPointMgr.LoadFromCsv(csv_path);
		}
		
		public TreeNode CreateRandomNode(Tree tree, bool ShouldForceTerminal = false)
		{
			//TODO remove this completely
			return TreeNode.TreeNodeFactory(this, ShouldForceTerminal, tree);
		}

		//TODO move this somewhere else
		public Tree CreateRandomTree()
		{
			//build a random tree
			Tree tree = new Tree();
			
			TreeNode root = CreateRandomNode(tree);
			tree.AddRootToTree(root);
			
			//run a queue to create children for non-terminal nodes
			Queue<TreeNode> nonTermNodes = new Queue<TreeNode>();
			nonTermNodes.Enqueue(root);
			while (nonTermNodes.Count > 0)
			{
				var node = nonTermNodes.Dequeue();
				node.FillNodeWithRandomChildrenIfNeeded(this);
				
				foreach (var subNode in node._subNodes)
				{
					nonTermNodes.Enqueue(subNode);
				}
			}
			return tree;
		}
		
		public void DoSomePrediction()
		{
			//TODO this method is poorly named and a hack
			PredictionManager predMgr = new PredictionManager(this);
			
			predMgr.LoadTreeAndGenerateResults();
			
			OnProgressUpdated(100);
		}
		
		public void DoAllPredictions(string path)
		{
			//TODO this method is poorly named and a hack
			PredictionManager predMgr = new PredictionManager(this);
			
			predMgr.GeneratePredictionsForDataWithAllTrees(path);
			
			OnProgressUpdated(100);
		}
	}
}