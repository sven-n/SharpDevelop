﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;

using ICSharpCode.SharpDevelop.Project;
using NuGet;

namespace ICSharpCode.PackageManagement
{
	public class UpdatedPackages
	{
		IPackageRepository sourceRepository;
		IQueryable<IPackage> installedPackages;
		
		public UpdatedPackages(
			IPackageManagementProject project,
			IPackageRepository aggregateRepository)
			: this(
				project.GetPackages(),
				aggregateRepository)
		{
		}
		
		public UpdatedPackages(
			IQueryable<IPackage> installedPackages,
			IPackageRepository aggregrateRepository)
		{
			this.installedPackages = installedPackages;
			this.sourceRepository = aggregrateRepository;
		}
		
		public string SearchTerms { get; set; }
		
		public IEnumerable<IPackage> GetUpdatedPackages(bool includePrerelease = false)
		{
			IQueryable<IPackage> localPackages = installedPackages;
			localPackages = FilterPackages(localPackages);
			IEnumerable<IPackage> distinctLocalPackages = DistinctPackages(localPackages);
			return GetUpdatedPackages(sourceRepository, distinctLocalPackages, includePrerelease);
		}
		
		IQueryable<IPackage> GetInstalledPackages()
		{
			return installedPackages;
		}
		
		IQueryable<IPackage> FilterPackages(IQueryable<IPackage> localPackages)
		{
			return localPackages.Find(SearchTerms);
		}
		
		/// <summary>
		/// If we have jQuery 1.6 and 1.7 then return just jquery 1.6
		/// </summary>
		IEnumerable<IPackage> DistinctPackages(IQueryable<IPackage> localPackages)
		{
			List<IPackage> packages = localPackages.ToList();
			if (packages.Any()) {
				packages.Sort(PackageComparer.Version);
				return packages.Distinct<IPackage>(PackageEqualityComparer.Id).ToList();
			}
			return packages;
		}
		
		IEnumerable<IPackage> GetUpdatedPackages(
			IPackageRepository sourceRepository,
			IEnumerable<IPackage> localPackages,
			bool includePrelease)
		{
			return sourceRepository.GetUpdates(localPackages, includePrelease, false);
		}
	}
}
