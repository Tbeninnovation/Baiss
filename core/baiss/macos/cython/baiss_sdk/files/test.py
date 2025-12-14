import structures

outputDir = "../../../../raw_jsons/"

res = structures.cnativegen(
    targetPaths=["42fgh23fgh12fg", "file2", "file3"],
    outputDir=outputDir,
    outputFile=outputDir + "tree_structure.json"
)

print (res)
