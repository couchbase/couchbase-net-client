using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase
{
    public interface ISpatialViewRow
    {
        /// <summary>
        /// Returns the id of the item referenced by this row. This id can be used to
        /// perform the standard (Set/Get/Remove/etc.) operations on the item.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Bounding box of indexed coordinates from document
        /// </summary>
        float[] BoundingBox { get; set; }

        /// <summary>
        /// Structure containing geometry data (e.g., coordinates, type).
        /// </summary>
        SpatialViewGeometry Geometry { get; set; }

        /// <summary>
        /// Value emitted by map function
        /// </summary>
        object Value { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion