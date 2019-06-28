using System;
using System.Collections.Generic;
using System.Text;

namespace GitRewrite.GitObjects
{
    class TreeLineByHashComparer : IEqualityComparer<Tree.TreeLine>
    {
        public bool Equals(Tree.TreeLine x, Tree.TreeLine y) => x.Hash.Equals(y.Hash);

        public int GetHashCode(Tree.TreeLine obj) => obj.Hash.GetHashCode();
    }
}
