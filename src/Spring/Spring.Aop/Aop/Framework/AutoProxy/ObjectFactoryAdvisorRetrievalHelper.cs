#region License

/*
 * Copyright 2002-2009 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections;
using Common.Logging;
using Spring.Objects.Factory;
using Spring.Objects.Factory.Config;
using Spring.Util;

namespace Spring.Aop.Framework.AutoProxy
{
    /// <summary>
    /// Helper for retrieving standard Spring advisors from an <see cref="IObjectFactory"/> for
    /// use with auto-proxying.
    /// </summary>
    /// <author>Erich Eichinger</author>
    public class ObjectFactoryAdvisorRetrievalHelper : IAdvisorRetrievalHelper
    {
        private readonly ILog _log;
        private readonly IConfigurableListableObjectFactory _objectFactory;
        private string[] _cachedObjectNames;

        /// <summary>
        /// The object factory to lookup advisors from
        /// </summary>
        public IConfigurableListableObjectFactory ObjectFactory
        {
            get { return _objectFactory; }
        }

        /// <summary>
        /// Create a new helper for the specified <paramref name="objectFactory"/>.
        /// </summary>
        public ObjectFactoryAdvisorRetrievalHelper(IConfigurableListableObjectFactory objectFactory )
        {
            AssertUtils.ArgumentNotNull(objectFactory, "objectFactory");
            _log = LogManager.GetLogger(this.GetType());
            _objectFactory = objectFactory;
        }

        /// <summary>
        /// Find all all eligible advisor objects in the current object factory.
        /// </summary>
        /// <param name="targetType">the type of the object to be advised</param>
        /// <param name="targetName">the name of the object to be advised</param>
        /// <returns>A list of eligible <see cref="IAdvisor"/> instances</returns>
        public virtual IList FindAdvisorObjects(Type targetType, string targetName)
        {
            string[] advisorNames = GetAdvisorCandidateNames(targetType, targetName);

            ArrayList advisors = new ArrayList();

            if (advisorNames.Length == 0)
            {
                return advisors;
            }
                
            for (int i = 0; i < advisorNames.Length; i++)
            {
                string name = advisorNames[i];
                if (IsEligibleObject(name, targetType, targetName) && !_objectFactory.IsCurrentlyInCreation(name))
                {
                    try
                    {
                        AddAdvisorCandidate(advisors, name);
                    }
                    catch (ObjectCreationException ex)
                    {
                        Exception rootEx = ex.GetBaseException();
                        if (rootEx is ObjectCurrentlyInCreationException)
                        {
                            ObjectCurrentlyInCreationException oce = (ObjectCurrentlyInCreationException)rootEx;
                            if (_objectFactory.IsCurrentlyInCreation(oce.ObjectName))
                            {
                                if (_log.IsDebugEnabled)
                                {
                                    _log.Debug(string.Format("Ignoring currently created advisor '{0}': exception message = {1}",
                                        name, ex.Message));
                                }
                                continue;
                            }
                        }
                        throw;
                    }
                }
            }

            return advisors;
        }

        /// <summary>
        /// Add the named advisor instance to the list of advisors.
        /// </summary>
        /// <param name="advisors">the advisor list</param>
        /// <param name="advisorName">the object name of the advisor to add</param>
        private void AddAdvisorCandidate(ArrayList advisors, string advisorName)
        {
            object advisorCandidate = _objectFactory.GetObject(advisorName);
            if (advisorCandidate is IAdvisor)
            {
                advisors.Add(advisorCandidate);
            } 
            else if (advisorCandidate is IAdvisors)
            {
                advisors.AddRange(((IAdvisors)advisorCandidate).Advisors);
            }
            else
            {
                throw new InvalidOperationException("expected type IAdvisor or IAdvisors but was " +
                                                    advisorCandidate.GetType().FullName);
            }
        }

        /// <summary>
        /// Gets the names of advisor candidates
        /// </summary>
        /// <param name="targetType">the type of the object to be advised</param>
        /// <param name="targetName">the name of the object to be advised</param>
        /// <returns>a non-null string array of advisor candidate names</returns>
        protected virtual string[] GetAdvisorCandidateNames(Type targetType, string targetName)
        {
            if (_cachedObjectNames == null)
            {
                lock(this)
                {
                    if (_cachedObjectNames == null)
                    {
                        ArrayList candidateNameList = new ArrayList();
                        string[] advisorCandidateNames = ObjectFactoryUtils.ObjectNamesForTypeIncludingAncestors( _objectFactory, typeof(IAdvisor), true, false);
                        candidateNameList.AddRange(advisorCandidateNames);
                        string[] advisorsCandidateNames = ObjectFactoryUtils.ObjectNamesForTypeIncludingAncestors(_objectFactory, typeof(IAdvisors), true, false);
                        candidateNameList.AddRange(advisorsCandidateNames);
                        _cachedObjectNames = (string[]) candidateNameList.ToArray(typeof(string));
                    }
                }
            }
            return _cachedObjectNames;
        }

        /// <summary>
        /// Determine, whether the specified aspect object is eligible.
        /// The default implementation accepts all except for advisors that are 
        /// part of the internal infrastructure.
        /// </summary>
        /// <param name="advisorName">the name of the candidate advisor</param>
        /// <param name="objectType">the type of the object to be advised</param>
        /// <param name="objectName">the name of the object to be advised</param>
        protected virtual bool IsEligibleObject(string advisorName, Type objectType, string objectName )
        {
            return this.ObjectFactory.ContainsObjectDefinition(advisorName)
                   && this.ObjectFactory.GetObjectDefinition(advisorName).Role != ObjectRole.ROLE_INFRASTRUCTURE;
       }
    }
}