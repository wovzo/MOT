import 'package:flutter/material.dart';
import '../../../../core/network_client.dart';
import '../../data/models/habit_model.dart';
import '../../data/repositories/habit_repository.dart';

class HabitDashboardScreen extends StatefulWidget {
  const HabitDashboardScreen({super.key});

  @override
  State<HabitDashboardScreen> createState() => _HabitDashboardScreenState();
}

class _HabitDashboardScreenState extends State<HabitDashboardScreen> {
  late final HabitRepository _repository;
  List<Habit> _habits = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _repository = HabitRepository(NetworkClient());
    _loadHabits();
  }

  Future<void> _loadHabits() async {
    setState(() => _isLoading = true);
    try {
      final habits = await _repository.getHabits();
      setState(() {
        _habits = habits;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error loading habits: $e')),
        );
      }
    }
  }

  void _showAddHabitSheet() {
    final titleController = TextEditingController();
    final descController = TextEditingController();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
          left: 16,
          right: 16,
          top: 16,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Add New Habit', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            TextField(
              controller: titleController,
              decoration: const InputDecoration(labelText: 'Habit Title (e.g. Drink Water)', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: descController,
              decoration: const InputDecoration(labelText: 'Description (Optional)', border: OutlineInputBorder()),
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: () async {
                if (titleController.text.trim().isEmpty) return;
                Navigator.pop(context);
                
                try {
                  await _repository.createHabit(titleController.text.trim(), descController.text.trim());
                  _loadHabits();
                } catch (e) {
                  if (mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Failed to create habit: $e')),
                    );
                  }
                }
              },
              child: const Text('Save Habit'),
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Daily Habits'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadHabits,
          )
        ],
      ),
      body: _isLoading 
        ? const Center(child: CircularProgressIndicator())
        : _habits.isEmpty 
          ? const Center(child: Text('No habits yet. Start a new routine today!'))
          : ListView.builder(
              itemCount: _habits.length,
              itemBuilder: (context, index) {
                final habit = _habits[index];
                return Card(
                  margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    leading: InkWell(
                      onTap: () async {
                        try {
                          await _repository.toggleHabit(habit.id);
                          _loadHabits();
                        } catch (e) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('Failed to update habit')),
                          );
                        }
                      },
                      child: Container(
                        width: 32,
                        height: 32,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: Border.all(
                            color: habit.isCompletedToday ? Colors.green : Colors.grey,
                            width: 2,
                          ),
                          color: habit.isCompletedToday ? Colors.green : Colors.transparent,
                        ),
                        child: habit.isCompletedToday 
                          ? const Icon(Icons.check, size: 20, color: Colors.white)
                          : null,
                      ),
                    ),
                    title: Text(
                      habit.title,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    subtitle: habit.description.isNotEmpty 
                      ? Text(habit.description) 
                      : null,
                    trailing: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                      decoration: BoxDecoration(
                        color: Colors.orange.withOpacity(0.2),
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('🔥', style: TextStyle(fontSize: 16)),
                          const SizedBox(width: 4),
                          Text(
                            '${habit.currentStreak}',
                            style: const TextStyle(
                              color: Colors.orange,
                              fontWeight: FontWeight.bold,
                              fontSize: 16,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
      floatingActionButton: FloatingActionButton(
        onPressed: _showAddHabitSheet,
        child: const Icon(Icons.add),
      ),
    );
  }
}
