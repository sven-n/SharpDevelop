﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.PackageManagement.Scripting;
using ICSharpCode.SharpDevelop.Project;
using NuGet;

namespace ICSharpCode.PackageManagement
{
	public class SharpDevelopPackageManager : PackageManager, ISharpDevelopPackageManager
	{
		IProjectSystem projectSystem;
		IPackageOperationResolverFactory packageOperationResolverFactory;
		
		public SharpDevelopPackageManager(
			IPackageRepository sourceRepository,
			IProjectSystem projectSystem,
			ISolutionPackageRepository solutionPackageRepository,
			IPackageOperationResolverFactory packageOperationResolverFactory)
			: base(
				sourceRepository,
				solutionPackageRepository.PackagePathResolver,
				solutionPackageRepository.FileSystem,
				solutionPackageRepository.Repository)
		{
			this.projectSystem = projectSystem;
			this.packageOperationResolverFactory = packageOperationResolverFactory;
			CreateProjectManager();
		}
		
		// <summary>
		/// project manager should be created with:
		/// 	local repo = PackageReferenceRepository(projectSystem, sharedRepo)
		///     packageRefRepo should have its RegisterIfNecessary() method called before creating the project manager.
		/// 	source repo = sharedRepository
		/// </summary>
		void CreateProjectManager()
		{
			var packageRefRepository = CreatePackageReferenceRepository();
			ProjectManager = CreateProjectManager(packageRefRepository);
		}
		
		PackageReferenceRepository CreatePackageReferenceRepository()
		{
			var sharedRepository = LocalRepository as ISharedPackageRepository;
			var packageRefRepository = new PackageReferenceRepository(projectSystem, projectSystem.ProjectName, sharedRepository);
			packageRefRepository.RegisterIfNecessary();
			return packageRefRepository;
		}
		
		public ISharpDevelopProjectManager ProjectManager { get; set; }
		
		SharpDevelopProjectManager CreateProjectManager(PackageReferenceRepository packageRefRepository)
		{
			return new SharpDevelopProjectManager(LocalRepository, PathResolver, projectSystem, packageRefRepository);
		}
		
		public void InstallPackage(IPackage package)
		{
			bool ignoreDependencies = false;
			bool allowPreleaseVersions = false;
			InstallPackage(package, ignoreDependencies, allowPreleaseVersions);
		}
		
		public void InstallPackage(IPackage package, InstallPackageAction installAction)
		{
			RunPackageOperations(installAction.Operations);
			AddPackageReference(package, installAction.IgnoreDependencies, installAction.AllowPrereleaseVersions);
		}
		
		void AddPackageReference(IPackage package, bool ignoreDependencies, bool allowPrereleaseVersions)
		{
			var monitor = new RemovedPackageReferenceMonitor(ProjectManager);
			using (monitor) {
				ProjectManager.AddPackageReference(package.Id, package.Version, ignoreDependencies, allowPrereleaseVersions);
			}
			
			monitor.PackagesRemoved.ForEach(packageRemoved => UninstallPackageFromSolutionRepository(packageRemoved));
		}
		
		public override void InstallPackage(IPackage package, bool ignoreDependencies, bool allowPrereleaseVersions)
		{
			base.InstallPackage(package, ignoreDependencies, allowPrereleaseVersions);
			AddPackageReference(package, ignoreDependencies, allowPrereleaseVersions);
		}
		
		public void UninstallPackage(IPackage package, UninstallPackageAction uninstallAction)
		{
			UninstallPackage(package, uninstallAction.ForceRemove, uninstallAction.RemoveDependencies);
		}
		
		public override void UninstallPackage(IPackage package, bool forceRemove, bool removeDependencies)
		{
			ProjectManager.RemovePackageReference(package.Id, forceRemove, removeDependencies);
			if (!IsPackageReferencedByOtherProjects(package)) {
				base.UninstallPackage(package, forceRemove, removeDependencies);
			}
		}
		
		public void UninstallPackageFromSolutionRepository(IPackage package)
		{
			if (!IsPackageReferencedByOtherProjects(package)) {
				ExecuteUninstall(package);
			}
		}
		
		bool IsPackageReferencedByOtherProjects(IPackage package)
		{
			var sharedRepository = LocalRepository as ISharedPackageRepository;
			return sharedRepository.IsReferenced(package.Id, package.Version);
		}
		
		public IEnumerable<PackageOperation> GetInstallPackageOperations(IPackage package, InstallPackageAction installAction)
		{
			IPackageOperationResolver resolver = CreateInstallPackageOperationResolver(installAction);
			return resolver.ResolveOperations(package);
		}
		
		IPackageOperationResolver CreateInstallPackageOperationResolver(InstallPackageAction installAction)
		{
			return packageOperationResolverFactory.CreateInstallPackageOperationResolver(
				LocalRepository,
				SourceRepository,
				Logger,
				installAction);
		}
		
		public void UpdatePackage(IPackage package, UpdatePackageAction updateAction)
		{
			RunPackageOperations(updateAction.Operations);
			UpdatePackageReference(package, updateAction);
		}
		
		public void UpdatePackageReference(IPackage package, IUpdatePackageSettings settings)
		{
			UpdatePackageReference(package, settings.UpdateDependencies, settings.AllowPrereleaseVersions);
		}
		
		void UpdatePackageReference(IPackage package, bool updateDependencies, bool allowPrereleaseVersions)
		{
			var monitor = new RemovedPackageReferenceMonitor(ProjectManager);
			using (monitor) {
				ProjectManager.UpdatePackageReference(package.Id, package.Version, updateDependencies, allowPrereleaseVersions);
			}
			
			monitor.PackagesRemoved.ForEach(packageRemoved => UninstallPackageFromSolutionRepository(packageRemoved));
		}
		
		public void UpdatePackages(UpdatePackagesAction updateAction)
		{
			RunPackageOperations(updateAction.Operations);
			foreach (IPackage package in updateAction.Packages) {
				UpdatePackageReference(package, updateAction);
			}
		}
		
		public IEnumerable<PackageOperation> GetUpdatePackageOperations(
			IEnumerable<IPackage> packages,
			IUpdatePackageSettings settings)
		{
			IPackageOperationResolver resolver = CreateUpdatePackageOperationResolver(settings);
			
			var reducedOperations = new ReducedPackageOperations(resolver, packages);
			reducedOperations.Reduce();
			return reducedOperations.Operations;
		}
		
		IPackageOperationResolver CreateUpdatePackageOperationResolver(IUpdatePackageSettings settings)
		{
			return packageOperationResolverFactory.CreateUpdatePackageOperationResolver(
				LocalRepository,
				SourceRepository,
				Logger,
				settings);
		}
		
		public void RunPackageOperations(IEnumerable<PackageOperation> operations)
		{
			foreach (PackageOperation operation in operations) {
				Execute(operation);
			}
		}
	}
}
